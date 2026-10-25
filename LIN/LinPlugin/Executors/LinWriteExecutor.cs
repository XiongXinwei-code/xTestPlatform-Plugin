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
        try
        {
            var guard = new LIN.Runtime.LinExecutionGuard(context, cancellationToken);
            var step = context.CurrentStep!.Step;
            var serializer = new LinWritePlugin().CreateSerializer();
            var setting = (LinWriteSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            var connName = await Evaluator.EvalStringAsync(setting.ConnectionName, context);
            if (setting.TimeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(setting.TimeoutMs), "驱动调用超时必须大于 0");
            var key = LinHelper.GetAdapterKey(connName);
            if (!context.Resources.TryGet<ILinAdapter>(key, out var adapter))
            {
                guard.Check();
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Error,
                        Error  = new ErrorInfo { Message = $"LIN 连接未找到: {connName}，请先执行 LIN_Open 步骤" }
                    }
                };
            }
            guard.Watch(key, adapter);

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

            guard.Check();
            await Task.Run(() => LIN.Runtime.LinConnectionResource.For(adapter).Write(frame, cancellationToken, setting.TimeoutMs, cancelWait: true, guard.Check), cancellationToken);

            guard.Log($"LIN 发送: ID=0x{frameId:X2}({frameId}), Data=[{BitConverter.ToString(data).Replace("-", " ")}]");

            guard.Check();

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Completed,
                    Value  = $"0x{frameId:X2}: {BitConverter.ToString(data).Replace("-", " ")}"
                }
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
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
                    Error  = ErrorInfo.FromException(ex, $"LIN 发送失败: {ex.Message}")
                }
            };
        }
    }
}
