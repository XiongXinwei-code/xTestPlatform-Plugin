using CAN.XCP.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.XCP.Executors;

public sealed class XcpShortUploadExecutor : IStepExecutor
{
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new XcpShortUploadPlugin().CreateSerializer();
        var setting = (XcpShortUploadSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            var (client, error) = await XcpExecutorHelper.CreateClientAsync(setting, context, cancellationToken);
            if (client == null)
                return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Error, Error = new ErrorInfo { Message = error! } } };

            if (setting.ReadLength is < 1 or > 7)
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Error,
                        Error  = new ErrorInfo { Message = "ReadLength 必须在 1-7 之间" }
                    }
                };

            var addrStr = await XcpExecutorHelper.EvalStringAsync(setting.Address, context);
            uint address = XcpExecutorHelper.ParseAddress(addrStr);
            byte addrExt = (byte)setting.AddressExtension;

            var data = await client.ShortUploadAsync(address, addrExt, (byte)setting.ReadLength, cancellationToken);

            // 根据字节序处理
            if (setting.ByteOrder == XcpByteOrder.BigEndian)
                Array.Reverse(data);

            var hexValue = XcpExecutorHelper.ToHex(data);

            if (setting.EnableLog)
                context.LogAction?.Invoke($"XCP SHORT_UPLOAD 0x{address:X8} [{setting.ReadLength}字节] = {hexValue}");

            if (!string.IsNullOrWhiteSpace(setting.ResultVariable))
                context.SetVariable(setting.ResultVariable, hexValue);

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Passed,
                    Value  = hexValue
                }
            };
        }
        catch (OperationCanceledException)
        {
            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Aborted } };
        }
        catch (Exception ex)
        {
            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Error,
                    Error  = ErrorInfo.FromException(ex, $"XCP SHORT_UPLOAD 失败: {ex.Message}")
                }
            };
        }
    }
}
