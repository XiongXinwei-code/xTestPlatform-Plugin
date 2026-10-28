using CAN.Adapters;
using CAN.Helpers;
using CAN.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace CAN.Executors;

public sealed class CanReadExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var guard = new CAN.Runtime.CanExecutionGuard(context, cancellationToken);
            var step = context.CurrentStep!.Step;
            var serializer = new CanReadPlugin().CreateSerializer();
            var setting = (CanReadSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            var connName = await Evaluator.EvalStringAsync(setting.ConnectionName, context);
            var key = CanHelper.GetAdapterKey(connName);
            if (!context.Resources.TryGet<ICanAdapter>(key, out var adapter))
            {
                guard.Check();
                return ExecutionResults.Error($"CAN 连接未找到: {connName}");
            }

            // 解析过滤 ID
            guard.Watch(key, adapter);
            uint? filterId = null;
            if (!string.IsNullOrWhiteSpace(setting.FilterId))
            {
                var idStr = await Evaluator.EvalStringAsync(setting.FilterId, context);
                if (!string.IsNullOrWhiteSpace(idStr))
                    filterId = ParseCanId(idStr);
            }

            // 读取
            if (adapter is not CAN.Runtime.CanConnectionResource owner)
                throw new InvalidOperationException("旧 CAN 连接没有实际 I/O 生命周期所有者，请关闭后重新执行 CAN_Open");
            var msg = await guard.RunAsync(() => owner.ReadAsync(filterId, setting.ReadTimeoutMs, cancellationToken));

            if (msg == null)
            {
                return ExecutionResults.Error($"CAN 连接 {connName} 读取超时 ({setting.ReadTimeoutMs}ms)",
                    detail: $"过滤ID={(filterId.HasValue ? $"0x{filterId.Value:X}" : "不限")}");
            }

            var dataHex = BitConverter.ToString(msg.Data).Replace("-", " ");

            // 存储结果
            if (!string.IsNullOrWhiteSpace(setting.ResultVariable))
                guard.SetVariable(setting.ResultVariable, dataHex);
            if (!string.IsNullOrWhiteSpace(setting.IdVariable))
                guard.SetVariable(setting.IdVariable, $"0x{msg.Id:X}");

            guard.Log($"CAN 接收: ID=0x{msg.Id:X}, Data=[{dataHex}]");

            return ExecutionResults.Completed($"0x{msg.Id:X}: {dataHex}");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ExecutionResults.Aborted();
        }
        catch (Exception ex)
        {
            return ExecutionResults.FromException(ex, $"CAN 读取失败: {ex.Message}");
        }
    }

    private static uint ParseCanId(string idStr)
    {
        idStr = idStr.Trim();
        if (idStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return Convert.ToUInt32(idStr[2..], 16);
        return uint.Parse(idStr);
    }
}
