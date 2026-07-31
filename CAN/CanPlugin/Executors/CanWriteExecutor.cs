using CAN.Adapters;
using CAN.Helpers;
using CAN.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace CAN.Executors;

public sealed class CanWriteExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new CanWritePlugin().CreateSerializer();
        var setting = (CanWriteSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            var key = CanHelper.GetAdapterKey(setting.ConnectionName);
            if (!context.CurrentStep.RuntimeData.TryGetValue(key, out var obj) || obj is not ICanAdapter adapter)
            {
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Error,
                        Error = new ErrorInfo { Message = $"CAN 连接未找到: {setting.ConnectionName}" }
                    }
                };
            }

            // 求值 CAN ID
            var canIdStr = await Evaluator.EvaluateAsync<string>(setting.CanId, context) ?? setting.CanId;
            uint canId = CanHelper.ParseCanId(canIdStr);

            // 求值数据
            var dataStr = await Evaluator.EvaluateAsync<string>(setting.Data, context) ?? setting.Data;
            byte[] data = CanHelper.ParseHexData(dataStr);

            var message = new CanMessage
            {
                Id = canId,
                FrameType = setting.FrameType,
                Data = data,
                IsFd = setting.UseFdFrame
            };

            adapter.Write(message);

            if (setting.EnableLog)
                context.LogAction?.Invoke($"CAN 发送: ID=0x{canId:X}, Data=[{BitConverter.ToString(data).Replace("-", " ")}]");

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Passed,
                    Value = $"0x{canId:X}: {BitConverter.ToString(data).Replace("-", " ")}"
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
                    Error = new ErrorInfo { Message = $"CAN 发送失败: {ex.Message}" }
                }
            };
        }
    }

    }
