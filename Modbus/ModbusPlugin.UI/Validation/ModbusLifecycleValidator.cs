using MessagePack;
using Modbus.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace Modbus.UI.Validation;

internal static class ModbusLifecycleValidator
{
    private static readonly MessagePackSerializerOptions _opts =
        MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);

    public static void CheckPrecedingConnect(
        List<Step> block, Step currentStep, string connectionName, List<StepSettingError> errors)
    {
        if (string.IsNullOrWhiteSpace(connectionName)) return;

        int currentIndex = block.IndexOf(currentStep);
        if (currentIndex <= 0) goto NotFound;

        for (int i = 0; i < currentIndex; i++)
        {
            var step = block[i];
            if (step.StepSetting.StepType != "IO.ModbusConnect") continue;

            try
            {
                var setting = MessagePackSerializer.Deserialize<ModbusConnectSetting>(
                    step.StepSetting.Setting, _opts);
                if (setting.ConnectionName == connectionName) return;
            }
            catch { }
        }

    NotFound:
        errors.Add(StepSettingError.Warning("MODBUS_LC01",
            $"在此步骤之前未找到针对连接 \"{connectionName}\" 的 Modbus.Connect 步骤"));
    }
}
