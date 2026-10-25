using CAN.UDS.Models;
using CAN.Helpers;
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
        try
        {
            var guard = new CAN.Runtime.CanExecutionGuard(context, cancellationToken);
            var step = context.CurrentStep!.Step;
            var serializer = new UdsRawRequestPlugin().CreateSerializer();
            var setting = (UdsRawRequestSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            var (client, error) = await UdsExecutorHelper.CreateClientAsync(setting, context, cancellationToken, guard);
            if (client == null)
                return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Error, Error = new ErrorInfo { Message = error! } } };

            var dataStr = await Evaluator.EvalStringAsync(setting.RequestData, context);
            byte[] requestData = ParseHex(dataStr);

            if (requestData.Length == 0)
                return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Error, Error = new ErrorInfo { Message = "请求数据为空" } } };
            bool securityAccess = requestData[0] == 0x27;
            string requestDisplay = securityAccess ? "27 [安全访问负载已隐藏]" : UdsExecutorHelper.ToHex(requestData);

            if (!setting.WaitResponse)
            {
                await guard.RunAsync(() => client.SendOnlyAsync(requestData, cancellationToken));
                guard.Log($"UDS RawRequest (无响应): [{requestDisplay}]");
                cancellationToken.ThrowIfCancellationRequested();
                return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Completed, Value = requestDisplay } };
            }

            var response = await guard.RunAsync(() => client.RequestAsync(requestData, cancellationToken));
            var responseHex = UdsExecutorHelper.ToHex(response.RawBytes);
            string responseDisplay = securityAccess ? "[安全访问响应已隐藏]" : responseHex;

            cancellationToken.ThrowIfCancellationRequested();
            if (!string.IsNullOrWhiteSpace(setting.ResultVariable))
                guard.SetVariable(setting.ResultVariable, responseHex);

            guard.Log(LogLevel.Trace, $"UDS RawRequest: TX=[{requestDisplay}] RX=[{responseDisplay}]");
            cancellationToken.ThrowIfCancellationRequested();

            if (response.IsPositive)
                return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Completed, Value = responseDisplay } };
            else if (securityAccess)
                return ExecutionResults.Error($"UDS 安全访问失败：{response.GetNrcDescription()}", detail: response.RequestDescription, value: responseDisplay);
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
        if (hex.Length % 2 != 0) throw new FormatException("UDS 十六进制字符数必须为偶数，未发送请求");
        var bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        return bytes;
    }
}
