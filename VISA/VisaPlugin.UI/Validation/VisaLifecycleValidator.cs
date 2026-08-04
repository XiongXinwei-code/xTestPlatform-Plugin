using MessagePack;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using VISA.Models;
using xTestPlatform.Core.SequenceModels;

namespace VISA.UI.Validation;

internal static class VisaLifecycleValidator
{
    private static readonly MessagePackSerializerOptions _opts =
        MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);

    public static void CheckPrecedingOpen(
        List<Step> block, Step currentStep, string connectionName, List<StepSettingError> errors)
    {
        if (string.IsNullOrWhiteSpace(connectionName)) return;

        int currentIndex = block.IndexOf(currentStep);
        if (currentIndex <= 0) goto NotFound;

        for (int i = 0; i < currentIndex; i++)
        {
            var step = block[i];
            if (step.StepSetting.StepType != "IO.VisaOpen") continue;

            try
            {
                var setting = MessagePackSerializer.Deserialize<VisaOpenSetting>(
                    step.StepSetting.Setting, _opts);
                if (setting.ConnectionName == connectionName) return;
            }
            catch { }
        }

    NotFound:
        errors.Add(StepSettingError.Warning("VISA_LC01",
            $"在此步骤之前未找到针对连接 \"{connectionName}\" 的 VISA.Open 步骤"));
    }
}
