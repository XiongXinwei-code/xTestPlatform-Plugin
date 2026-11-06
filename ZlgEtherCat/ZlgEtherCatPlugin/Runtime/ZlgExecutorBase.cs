using System.Diagnostics;
using System.Text.Json;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using ZlgEtherCat.Models;
using ZlgEtherCat.Contracts;

namespace ZlgEtherCat.Runtime;

public abstract class ZlgExecutorBase<T>(IStepPlugin plugin) : IStepExecutor where T : ZlgGroupedSetting
{
    protected virtual Task<object?> DispatchAsync(string operation, ZlgRequest request, IExecutionContext context, CancellationToken ct)
        => ZlgRuntime.ExecuteAsync(operation, request, context, ct);

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var registry = context.Resources;
            var epoch = PluginCommon.ExecutionEpoch.Capture(registry, "ZlgEtherCat");
            using var binding = epoch.Bind();
            var serialized = context.CurrentStep!.Step.StepSetting;
            var setting = (T)plugin.CreateSerializer().Deserialize(serialized.Setting, serialized.SettingVersion);
            var errors = setting.Validate();
            if (errors.Count > 0) throw new ArgumentException(string.Join("；", errors));
            // The settings whitelist resolves a single operation before evaluating any parameters.
            var operation = setting.ActiveOperation;
            if (!string.IsNullOrWhiteSpace(setting.ResultVariable) && !context.HasVariable(setting.ResultVariable))
                throw new ArgumentException($"结果变量 {setting.ResultVariable} 未声明");
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(setting.TimeoutMs);
            var watch = Stopwatch.StartNew();
            var request = await setting.ToRequestAsync(context, deadline.Token).ConfigureAwait(false);
            epoch.ThrowIfExpired();
            deadline.Token.ThrowIfCancellationRequested();
            var remaining = setting.TimeoutMs - (int)watch.ElapsedMilliseconds;
            if (remaining <= 0) throw new TimeoutException("表达式求值已超过步骤总期限");
            request = request with { TimeoutMs = remaining };
            var value = await DispatchAsync(operation, request, context, deadline.Token).ConfigureAwait(false);
            epoch.ThrowIfExpired();
            deadline.Token.ThrowIfCancellationRequested();
            if (!ReferenceEquals(registry, context.Resources)) throw new InvalidOperationException("执行作用域已切换，禁止写入旧结果");
            if (!string.IsNullOrWhiteSpace(request.ResultVariable)) context.SetVariable(request.ResultVariable, value!);
            return new() { StepResult = new() { Status = TestStatus.Completed, Value = JsonSerializer.Serialize(value) } };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        { return new() { StepResult = new() { Status = TestStatus.Aborted } }; }
        catch (Exception ex)
        {
            string message = ex is OperationCanceledException ? "ZLG EtherCAT 步骤超过配置期限；原生调用退出与设备动作须另行确认" : "ZLG EtherCAT 操作失败：" + ex.Message;
            return new() { StepResult = new() { Status = TestStatus.Error, Error = ErrorInfo.FromException(ex, message) } };
        }
    }
}
