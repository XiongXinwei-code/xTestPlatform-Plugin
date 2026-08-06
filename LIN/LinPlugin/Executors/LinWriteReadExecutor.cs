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
        var step = context.CurrentStep!.Step;
        var serializer = new LinWriteReadPlugin().CreateSerializer();
        var setting = (LinWriteReadSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            var connName = await Evaluator.EvalStringAsync(setting.ConnectionName, context);
            var key = LinHelper.GetAdapterKey(connName);
            if (!context.CurrentStep.RuntimeData.TryGetValue(key, out var obj) || obj is not ILinAdapter adapter)
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

            // 发送帧（主节点发送帧头 + 数据）
            var txFrame = new LinFrame
            {
                FrameId      = frameId,
                Data         = data,
                ChecksumType = setting.ChecksumType
            };
            adapter.Write(txFrame);

            if (setting.EnableLog)
                context.LogAction?.Invoke($"LIN 发送: ID=0x{frameId:X2}({frameId}), Data=[{BitConverter.ToString(data).Replace("-", " ")}]");

            // 等待从机对同一帧 ID 的响应
            var response = adapter.Read(frameId, setting.ResponseTimeoutMs, cancellationToken);

            if (response == null)
            {
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Failed,
                        Error  = new ErrorInfo { Message = $"等待从机响应超时 ({setting.ResponseTimeoutMs}ms)，帧 ID=0x{frameId:X2}" }
                    }
                };
            }

            var responseHex = BitConverter.ToString(response.Data).Replace("-", " ");

            if (!string.IsNullOrWhiteSpace(setting.ResultVariable))
                context.SetVariable(setting.ResultVariable, responseHex);

            if (setting.EnableLog)
                context.LogAction?.Invoke($"LIN 响应: ID=0x{response.FrameId:X2}({response.FrameId}), Data=[{responseHex}]");

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Passed,
                    Value  = $"请求 0x{frameId:X2} → 响应: {responseHex}"
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
                    Error  = new ErrorInfo { Message = $"LIN 发送并等待响应失败: {ex.Message}" }
                }
            };
        }
    }
}
