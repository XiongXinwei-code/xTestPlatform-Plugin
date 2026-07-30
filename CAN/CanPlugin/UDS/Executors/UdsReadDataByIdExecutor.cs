using CAN.UDS.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace CAN.UDS.Executors;

public sealed class UdsReadDataByIdExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new UdsReadDataByIdPlugin().CreateSerializer();
        var setting = (UdsReadDataByIdSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            var (client, error) = await UdsExecutorHelper.CreateClientAsync(setting, context, cancellationToken);
            if (client == null)
                return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Error, Error = new ErrorInfo { Message = error! } } };

            var didStr = await Evaluator.EvaluateAsync<string>(setting.Did, context) ?? setting.Did;
            ushort did = (ushort)UdsExecutorHelper.ParseId(didStr);

            var request = new byte[] { 0x22, (byte)(did >> 8), (byte)(did & 0xFF) };
            var response = await client.RequestAsync(request, cancellationToken);

            if (response.IsPositive)
            {
                // 响应: 62 [DID_H] [DID_L] [Data...]
                byte[] data = response.Data.Length > 2 ? response.Data[2..] : [];
                var hex = UdsExecutorHelper.ToHex(data);

                if (!string.IsNullOrWhiteSpace(setting.ResultVariable))
                    context.SetVariable(setting.ResultVariable, hex);

                context.LogAction?.Invoke($"UDS ReadDataByID: DID=0x{did:X4}, Data=[{hex}]");
                return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Passed, Value = hex } };
            }
            else
            {
                return new ExecutionResult
                {
                    StepResult = new StepResult { Status = TestStatus.Failed, Error = new ErrorInfo { Message = response.GetNrcDescription() }, Value = $"NRC=0x{response.NegativeResponseCode:X2}" }
                };
            }
        }
        catch (OperationCanceledException) { return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } }; }
        catch (Exception ex) { return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Error, Error = new ErrorInfo { Message = ex.Message } } }; }
    }
}
