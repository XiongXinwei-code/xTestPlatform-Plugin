using CAN.UDS.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace CAN.UDS.Executors;

public sealed class UdsRoutineControlExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new UdsRoutineControlPlugin().CreateSerializer();
        var setting = (UdsRoutineControlSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            var (client, error) = await UdsExecutorHelper.CreateClientAsync(setting, context, cancellationToken);
            if (client == null)
                return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Error, Error = new ErrorInfo { Message = error! } } };

            var ridStr = await Evaluator.EvaluateAsync<string>(setting.RoutineId, context) ?? setting.RoutineId;
            ushort rid = (ushort)UdsExecutorHelper.ParseId(ridStr);

            var optStr = await Evaluator.EvaluateAsync<string>(setting.OptionRecord, context) ?? setting.OptionRecord;
            byte[] optData = ParseHex(optStr);

            var request = new byte[4 + optData.Length];
            request[0] = 0x31;
            request[1] = (byte)setting.ControlType;
            request[2] = (byte)(rid >> 8);
            request[3] = (byte)(rid & 0xFF);
            if (optData.Length > 0)
                Buffer.BlockCopy(optData, 0, request, 4, optData.Length);

            var response = await client.RequestAsync(request, cancellationToken);

            if (response.IsPositive)
            {
                byte[] resultData = response.Data.Length > 3 ? response.Data[3..] : [];
                var hex = UdsExecutorHelper.ToHex(resultData);
                if (!string.IsNullOrWhiteSpace(setting.ResultVariable))
                    context.SetVariable(setting.ResultVariable, hex);
                context.LogAction?.Invoke($"UDS RoutineControl: {setting.ControlType} RID=0x{rid:X4} Result=[{hex}]");
                return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Passed, Value = hex } };
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
