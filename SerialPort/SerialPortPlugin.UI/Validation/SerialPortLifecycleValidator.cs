using MessagePack;
using SerialPort.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace SerialPort.UI.Validation;

internal static class SerialPortLifecycleValidator
{
    private static readonly MessagePackSerializerOptions _opts =
        MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);

    /// <summary>
    /// 检查当前步骤之前是否存在匹配的 SerialPort.Open 步骤
    /// </summary>
    public static void CheckPrecedingOpen(
        List<Step> block, Step currentStep, string portName, List<StepSettingError> errors)
    {
        if (string.IsNullOrWhiteSpace(portName)) return;

        int currentIndex = block.IndexOf(currentStep);
        if (currentIndex <= 0) goto NotFound;

        for (int i = 0; i < currentIndex; i++)
        {
            var step = block[i];
            if (step.StepSetting.StepType != "IO.SerialPortOpen") continue;

            try
            {
                var setting = MessagePackSerializer.Deserialize<SerialPortOpenSetting>(
                    step.StepSetting.Setting, _opts);
                if (setting.PortName == portName) return;
            }
            catch { /* 反序列化失败则跳过 */ }
        }

    NotFound:
        errors.Add(StepSettingError.Warning("SP_LC01",
            $"在此步骤之前未找到针对端口 \"{portName}\" 的 SerialPort.Open 步骤"));
    }
}
