using CAN.UDS.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace CAN.UDS.Executors;

public sealed class UdsWriteDataByIdExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new UdsWriteDataByIdPlugin().CreateSerializer();
        var setting = (UdsWriteDataByIdSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            var (client, error) = await UdsExecutorHelper.CreateClientAsync(setting, context, cancellationToken);
            if (client == null)
                return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Error, Error = new ErrorInfo { Message = error! } } };

            var didStr = await Evaluator.EvaluateAsync<string>(setting.Did, context) ?? setting.Did;
            ushort did = (ushort)UdsExecutorHelper.ParseId(didStr);

            var dataStr = await Evaluator.EvaluateAsync<string>(setting.Data, context) ?? setting.Data;
            byte[] data = ParseHex(dataStr);

            var request = new byte[3 + data.Length];
            request[0] = 0x2E;
            request[1] = (byte)(did >> 8);
            request[2] = (byte)(did & 0xFF);
            Buffer.BlockCopy(data, 0, request, 3, data.Length);

            var response = await client.RequestAsync(request, cancellationToken);

            if (response.IsPositive)
            {
                if (setting.EnableLog)
                    context.LogAction?.Invoke($"UDS WriteDataByID: DID=0x{did:X4} 写入成功");
                return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Passed, Value = $"DID=0x{did:X4}" } };
            }
            else
            {
                return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Failed, Error = new ErrorInfo { Message = response.GetNrcDescription() } } };
            }
        }
        catch (OperationCanceledException) { return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } }; }
        catch (Exception ex) { return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Error, Error = new ErrorInfo { Message = ex.Message } } }; }
    }

    private static byte[] ParseHex(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return [];
        hex = hex.Replace(" ", "").Replace("-", "").Replace(",", "");
        var bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        return bytes;
    }
}
