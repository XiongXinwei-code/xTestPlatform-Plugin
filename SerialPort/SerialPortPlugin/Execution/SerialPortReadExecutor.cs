using SerialPortPlugin.Models;
using SerialPortPlugin.Plugins;
using SerialPortPlugin.Services;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace SerialPortPlugin.Execution;

public sealed class SerialPortReadExecutor : IStepExecutor
{
    public async Task<ExecutionResult> ExecuteAsync(IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var step = context.CurrentStep!.Step;
        var serializer = new SerialPortReadPlugin().CreateSerializer();
        var s = (SerialPortReadSetting)serializer.Deserialize(step.StepSetting.Setting, serializer.SettingVersion);

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

            // 临时覆盖超时（如果用户设置了）
            var originalTimeout = port.ReadTimeout;
            if (s.TimeoutMs > 0)
                port.ReadTimeout = s.TimeoutMs;

            string result;
            try
            {
                context.LogAction?.Invoke($"从 {s.PortName} 读取数据，模式: {s.ReadMode}...");

                switch (s.ReadMode.ToUpperInvariant())
                {
                    case "BYTES":
                        var buffer = new byte[s.ByteCount];
                        int totalRead = 0;
                        while (totalRead < s.ByteCount)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            int read = port.Read(buffer, totalRead, s.ByteCount - totalRead);
                            totalRead += read;
                        }
                        result = s.Encoding.ToUpperInvariant() == "HEX"
                            ? BitConverter.ToString(buffer).Replace("-", " ")
                            : (s.Encoding.ToUpperInvariant() == "UTF8"
                                ? System.Text.Encoding.UTF8.GetString(buffer)
                                : System.Text.Encoding.ASCII.GetString(buffer));
                        break;

                    case "UNTIL":
                        result = ReadUntil(port, s.Terminator, cancellationToken);
                        break;

                    default: // Line
                        result = port.ReadLine();
                        break;
                }
            }
            finally
            {
                if (s.TimeoutMs > 0)
                    port.ReadTimeout = originalTimeout;
            }

            context.LogAction?.Invoke($"读取到: {result}");

            // 存入目标变量
            if (!string.IsNullOrWhiteSpace(s.TargetVariable))
                context.SetVariable(s.TargetVariable, result);

            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Passed,
                    Value = result
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
        catch (TimeoutException)
        {
            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Failed,
                    Error = new ErrorInfo { Message = $"串口 {s.PortName} 读取超时" }
                }
            };
        }
        catch (Exception ex)
        {
            return new ExecutionResult
            {
                StepResult = new StepResult
                {
                    Status = TestStatus.Error,
                    Error = new ErrorInfo { Message = $"串口读取失败: {ex.Message}" }
                }
            };
        }
    }

    private static string ReadUntil(System.IO.Ports.SerialPort port, string terminator, CancellationToken ct)
    {
        var sb = new System.Text.StringBuilder();
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            int ch = port.ReadChar();
            sb.Append((char)ch);
            if (sb.ToString().EndsWith(terminator))
            {
                sb.Length -= terminator.Length;
                break;
            }
        }
        return sb.ToString();
    }
}
