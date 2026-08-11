using LIN.Adapters;
using LIN.Helpers;
using LIN.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace LIN.Executors;

public sealed class LinWriteExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new LinWritePlugin().CreateSerializer();
        var setting = (LinWriteSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

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

            var frameIdStr = await Evaluator.EvalStringAsync(setting.FrameId, context);
            var dataStr    = await Evaluator.EvalStringAsync(setting.Data, context);

            byte frameId = LinHelper.ParseFrameId(frameIdStr);
            byte[] data  = LinHelper.ParseHexData(dataStr);

            var frame = new LinFrame
            {
                FrameId      = frameId,
                Data         = data,
                ChecksumType = setting.ChecksumType
            };

            adapter.Write(frame);

            if (setting.EnableLog)
                context.LogAction?.Invoke($"LIN 发送: ID=0x{frameId:X2}({frameId}), Data=[{BitConverter.ToString(data).Replace("-", " ")}]");

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Passed,
                    Value  = $"0x{frameId:X2}: {BitConverter.ToString(data).Replace("-", " ")}"
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
                    Error  = new ErrorInfo { Message = $"LIN 发送失败: {ex.Message}" }
                }
            };
        }
    }
}
