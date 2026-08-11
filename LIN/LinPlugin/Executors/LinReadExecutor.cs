using LIN.Adapters;
using LIN.Helpers;
using LIN.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace LIN.Executors;

public sealed class LinReadExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new LinReadPlugin().CreateSerializer();
        var setting = (LinReadSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            var connName = await Evaluator.EvalStringAsync(setting.ConnectionName, context);
            var key = LinHelper.GetAdapterKey(connName);
            if (!context.Resources.TryGet<ILinAdapter>(key, out var adapter))
            {
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Error,
                        Error  = new ErrorInfo { Message = $"LIN 连接未找到: {connName}，请先执行 LIN_Open 步骤" }
                    }
                };
            }

            // 解析过滤帧 ID
            byte? filterId = null;
            var filterIdStr = await Evaluator.EvalStringAsync(setting.FilterFrameId, context);
            if (!string.IsNullOrWhiteSpace(filterIdStr))
                filterId = LinHelper.ParseFrameId(filterIdStr);

            // 读取帧
            LinFrame? frame;
            if (filterId.HasValue)
                frame = adapter.Read(filterId.Value, setting.ReadTimeoutMs, cancellationToken);
            else
                frame = adapter.Read(setting.ReadTimeoutMs, cancellationToken);

            if (frame == null)
            {
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Failed,
                        Error  = new ErrorInfo { Message = $"读取超时 ({setting.ReadTimeoutMs}ms)，未收到 LIN 帧" }
                    }
                };
            }

            var dataHex = BitConverter.ToString(frame.Data).Replace("-", " ");

            if (!string.IsNullOrWhiteSpace(setting.ResultVariable))
                context.SetVariable(setting.ResultVariable, dataHex);
            if (!string.IsNullOrWhiteSpace(setting.IdVariable))
                context.SetVariable(setting.IdVariable, frame.FrameId.ToString());

            if (setting.EnableLog)
                context.LogAction?.Invoke($"LIN 接收: ID=0x{frame.FrameId:X2}({frame.FrameId}), Data=[{dataHex}]");

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Passed,
                    Value  = $"0x{frame.FrameId:X2}: {dataHex}"
                }
            };
        }
        catch (OperationCanceledException)
        {
            return new ExecutionResult
            {
                StepResult = new StepResult { Status = TestStatus.Aborted }
            };
        }
        catch (Exception ex)
        {
            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Error,
                    Error  = new ErrorInfo { Message = $"LIN 接收失败: {ex.Message}" }
                }
            };
        }
    }
}
