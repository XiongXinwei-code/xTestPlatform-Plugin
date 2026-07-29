using SerialPortPlugin.Models;
using SerialPortPlugin.Plugins;
using SerialPortPlugin.Services;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace SerialPortPlugin.Execution;

public sealed class SerialPortWriteExecutor : IStepExecutor
{
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new SerialPortWritePlugin().CreateSerializer();
        var s = (SerialPortWriteSetting)serializer.Deserialize(step.StepSetting.Setting, serializer.SettingVersion);

        try
        {
            if (string.IsNullOrWhiteSpace(s.PortName))
                return new ExecutionResult
                {
                    StepResult = new StepResult
                    {
                        Status = TestStatus.Error,
                        Error = new ErrorInfo { Message = "端口名不能为空" }
                    }
                };

            var port = SerialPortManager.Get(s.PortName);
            var data = s.Data;

            context.LogAction?.Invoke($"向 {s.PortName} 发送数据 ({s.Encoding}): {data}");

            switch (s.Encoding.ToUpperInvariant())
            {
                case "HEX":
                    var bytes = HexToBytes(data);
                    port.Write(bytes, 0, bytes.Length);
                    break;
                default:
                    var encoding = s.Encoding.ToUpperInvariant() == "UTF8"
                        ? System.Text.Encoding.UTF8
                        : System.Text.Encoding.ASCII;
                    if (s.AppendNewLine)
                        port.WriteLine(data);
                    else
                    {
                        var textBytes = encoding.GetBytes(data);
                        port.Write(textBytes, 0, textBytes.Length);
                    }
                    break;
            }

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Passed,
                    Value = data
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
                    Error = new ErrorInfo { Message = $"串口写入失败: {ex.Message}" }
                }
            };
        }
    }

    private static byte[] HexToBytes(string hex)
    {
        hex = hex.Replace(" ", "").Replace("-", "");
        var bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        return bytes;
    }
}
