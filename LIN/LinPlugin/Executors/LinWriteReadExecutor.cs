using LIN.Adapters;
using LIN.Helpers;
using LIN.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace LIN.Executors;

/// <summary>发送 LIN 帧后等待同一 ID 的从机响应</summary>
public sealed class LinWriteReadExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var guard = new LIN.Runtime.LinExecutionGuard(context, cancellationToken);
            var step = context.CurrentStep!.Step;
            var serializer = new LinWriteReadPlugin().CreateSerializer();
            var setting = (LinWriteReadSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            LinHelper.ValidateStringOutput(context, setting.ResultVariable, guard.Check);
            var connName = await Evaluator.EvalStringAsync(setting.ConnectionName, context);
            if (setting.SendTimeoutMs <= 0) throw new ArgumentOutOfRangeException(nameof(setting.SendTimeoutMs));
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

            guard.Check();
            // 发送帧（主节点发送帧头 + 数据）
            var txFrame = new LinFrame
            {
                FrameId      = frameId,
                Data         = data,
                ChecksumType = setting.ChecksumType
            };
            // One owner spans send, logging and response; no other step/cyclic sender
            // may steal the matching ID response between these two operations.
            var response = await LIN.Runtime.LinConnectionResource.For(adapter).WriteReadAsync(txFrame,
                setting.SendTimeoutMs, setting.ResponseTimeoutMs,
                () => guard.Log($"LIN 发送: ID=0x{frameId:X2}({frameId}), Data=[{BitConverter.ToString(data).Replace("-", " ")}]"), cancellationToken, guard.Check);
            cancellationToken.ThrowIfCancellationRequested();

            if (response == null)
            {
                guard.Check();
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Error,
                        Error  = new ErrorInfo { Message = $"等待从机响应超时 ({setting.ResponseTimeoutMs}ms)，帧 ID=0x{frameId:X2}" }
                    }
                };
            }

            var responseHex = BitConverter.ToString(response.Data).Replace("-", " ");

            if (!string.IsNullOrWhiteSpace(setting.ResultVariable))
                guard.SetVariable(setting.ResultVariable, responseHex);

            guard.Log($"LIN 响应: ID=0x{response.FrameId:X2}({response.FrameId}), Data=[{responseHex}]");

            guard.Check();

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Completed,
                    Value  = $"请求 0x{frameId:X2} → 响应: {responseHex}"
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
                    Error  = ErrorInfo.FromException(ex, $"LIN 发送并等待响应失败: {ex.Message}")
                }
            };
        }
    }
}
