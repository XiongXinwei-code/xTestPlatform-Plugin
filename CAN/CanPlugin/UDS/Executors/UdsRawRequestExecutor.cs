using CAN.UDS.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace CAN.UDS.Executors;

public sealed class UdsRawRequestExecutor : IStepExecutor
{
    private static readonly IExpressionEvaluator Evaluator = ExpressionEvaluatorFactory.Default;

    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new UdsRawRequestPlugin().CreateSerializer();
        var setting = (UdsRawRequestSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            var (client, error) = await UdsExecutorHelper.CreateClientAsync(setting, context, cancellationToken);
            if (client == null)
                return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Error, Error = new ErrorInfo { Message = error! } } };

            var dataStr = await Evaluator.EvaluateAsync<string>(setting.RequestData, context) ?? setting.RequestData;
            byte[] requestData = ParseHex(dataStr);

            if (requestData.Length == 0)
                return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Error, Error = new ErrorInfo { Message = "请求数据为空" } } };

            if (!setting.WaitResponse)
            {
                await client.SendOnlyAsync(requestData, cancellationToken);
                context.Log($"UDS RawRequest (无响应): [{UdsExecutorHelper.ToHex(requestData)}]");
                return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Completed, Value = UdsExecutorHelper.ToHex(requestData) } };
            }

            var response = await client.RequestAsync(requestData, cancellationToken);
            var responseHex = UdsExecutorHelper.ToHex(response.RawBytes);

            if (!string.IsNullOrWhiteSpace(setting.ResultVariable))
                context.SetVariable(setting.ResultVariable, responseHex);

            context.Log(LogLevel.Trace, $"UDS RawRequest: TX=[{UdsExecutorHelper.ToHex(requestData)}] RX=[{responseHex}]");

            if (response.IsPositive)
                return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Completed, Value = responseHex } };
            else
                return UdsExecutorHelper.Failure($"UDS 原始请求 SID=0x{requestData[0]:X2}", response, responseHex);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        { return ExecutionResults.Aborted(); }
        catch (Exception ex) { return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Error, Error = ErrorInfo.FromException(ex) } }; }
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
