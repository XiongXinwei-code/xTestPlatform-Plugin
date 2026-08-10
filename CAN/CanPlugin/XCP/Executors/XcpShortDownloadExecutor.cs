using CAN.XCP.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN.XCP.Executors;

public sealed class XcpShortDownloadExecutor : IStepExecutor
{
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new XcpShortDownloadPlugin().CreateSerializer();
        var setting = (XcpShortDownloadSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);

        try
        {
            var (client, error) = await XcpExecutorHelper.CreateClientAsync(setting, context, cancellationToken);
            if (client == null)
                return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Error, Error = new ErrorInfo { Message = error! } } };

            var addrStr  = await XcpExecutorHelper.EvalStringAsync(setting.Address, context);
            var dataStr  = await XcpExecutorHelper.EvalStringAsync(setting.Data, context);

            uint   address = XcpExecutorHelper.ParseAddress(addrStr);
            byte[] data    = XcpExecutorHelper.ParseHexData(dataStr);
            byte   addrExt = (byte)setting.AddressExtension;

            if (data.Length is < 1 or > 6)
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Error,
                        Error  = new ErrorInfo { Message = $"SHORT_DOWNLOAD 数据长度 {data.Length} 超出范围（1-6字节）" }
                    }
                };

            // 根据字节序处理
            if (setting.ByteOrder == XcpByteOrder.BigEndian)
            {
                var reversed = (byte[])data.Clone();
                Array.Reverse(reversed);
                data = reversed;
            }

            await client.ShortDownloadAsync(address, addrExt, data, cancellationToken);

            if (setting.EnableLog)
                context.LogAction?.Invoke($"XCP SHORT_DOWNLOAD 0x{address:X8} ← {XcpExecutorHelper.ToHex(data)}");

            return new ExecutionResult { StepResult = new StepResult { Status = TestStatus.Passed } };
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
                    Error  = new ErrorInfo { Message = $"XCP SHORT_DOWNLOAD 失败: {ex.Message}" }
                }
            };
        }
    }
}
