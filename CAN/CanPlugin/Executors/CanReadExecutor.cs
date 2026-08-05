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
        var step = context.CurrentStep!.Step;
        var serializer = new CanReadPlugin().CreateSerializer();
        var setting = (CanReadSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            var connName = await Evaluator.EvaluateAsync<string>(setting.ConnectionName, context) ?? setting.ConnectionName;
            var key = CanHelper.GetAdapterKey(connName);
            if (!context.CurrentStep.RuntimeData.TryGetValue(key, out var obj) || obj is not ICanAdapter adapter)
            {
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Error,
                        Error = new ErrorInfo { Message = $"CAN 连接未找到: {connName}" }
                    }
                };
            }

            // 解析过滤 ID
            uint? filterId = null;
            if (!string.IsNullOrWhiteSpace(setting.FilterId))
            {
                var idStr = await Evaluator.EvaluateAsync<string>(setting.FilterId, context) ?? setting.FilterId;
                filterId = ParseCanId(idStr);
            }

            // 读取
            CanMessage? msg;
            if (filterId.HasValue)
                msg = adapter.Read(filterId.Value, setting.ReadTimeoutMs, cancellationToken);
            else
                msg = adapter.Read(setting.ReadTimeoutMs, cancellationToken);

            if (msg == null)
            {
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Failed,
                        Error = new ErrorInfo { Message = $"读取超时 ({setting.ReadTimeoutMs}ms)" }
                    }
                };
            }

            var dataHex = BitConverter.ToString(msg.Data).Replace("-", " ");

            // 存储结果
            if (!string.IsNullOrWhiteSpace(setting.ResultVariable))
                context.SetVariable(setting.ResultVariable, dataHex);
            if (!string.IsNullOrWhiteSpace(setting.IdVariable))
                context.SetVariable(setting.IdVariable, $"0x{msg.Id:X}");

            if (setting.EnableLog)
                context.LogAction?.Invoke($"CAN 接收: ID=0x{msg.Id:X}, Data=[{dataHex}]");

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Passed,
                    Value = $"0x{msg.Id:X}: {dataHex}"
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
                    Error = new ErrorInfo { Message = $"CAN 读取失败: {ex.Message}" }
                }
            };
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
