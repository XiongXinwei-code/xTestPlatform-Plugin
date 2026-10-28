using CAN.UDS.Models;
using CAN.Helpers;
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
        try
        {
            var guard = new CAN.Runtime.CanExecutionGuard(context, cancellationToken);
            var step = context.CurrentStep!.Step;
            var serializer = new UdsRoutineControlPlugin().CreateSerializer();
            var setting = (UdsRoutineControlSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            var (client, error) = await UdsExecutorHelper.CreateClientAsync(setting, context, cancellationToken, guard);
            if (client == null)
                return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Error, Error = new ErrorInfo { Message = error! } } };

            var ridStr = await Evaluator.EvalStringAsync(setting.RoutineId, context);
            ushort rid = (ushort)UdsExecutorHelper.ParseBoundedId(ridStr, ushort.MaxValue, "RoutineId");

            var optStr = await Evaluator.EvalStringAsync(setting.OptionRecord, context);
            byte[] optData = ParseHex(optStr);

            var request = new byte[4 + optData.Length];
            request[0] = 0x31;
            request[1] = (byte)setting.ControlType;
            request[2] = (byte)(rid >> 8);
            request[3] = (byte)(rid & 0xFF);
            if (optData.Length > 0)
                Buffer.BlockCopy(optData, 0, request, 4, optData.Length);

            var response = await guard.RunAsync(() => client.RequestAsync(request, cancellationToken));

            if (response.IsPositive)
            {
                byte[] resultData = response.Data.Length > 3 ? response.Data[3..] : [];
                var hex = UdsExecutorHelper.ToHex(resultData);
                if (!string.IsNullOrWhiteSpace(setting.ResultVariable))
                    guard.SetVariable(setting.ResultVariable, hex);
                guard.Log($"UDS RoutineControl: {setting.ControlType} RID=0x{rid:X4} Result=[{hex}]");
                return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Completed, Value = hex } };
            }
            else
            {
                return UdsExecutorHelper.Failure($"UDS 例程 {setting.ControlType}，RID=0x{rid:X4}", response);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        { return ExecutionResults.Aborted(); }
        catch (Exception ex) { return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Error, Error = ErrorInfo.FromException(ex) } }; }
    }

    private static byte[] ParseHex(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return [];
        hex = hex.Replace(" ", "").Replace("-", "").Replace(",", "");
        if (hex.Length % 2 != 0) throw new FormatException("UDS 十六进制字符数必须为偶数，未发送请求");
        var bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        return bytes;
    }
}
