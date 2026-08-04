using CAN;
using CAN.Models;
using MessagePack;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI.Validation;

internal static class CanLifecycleValidator
{
    private static readonly MessagePackSerializerOptions _opts =
        MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);

    /// <summary>
    /// 检查当前步骤之前是否存在匹配的 CAN.Open 步骤
    /// </summary>
    public static void CheckPrecedingOpen(
        List<Step> block, Step currentStep, string connectionName, List<StepSettingError> errors)
    {
        if (string.IsNullOrWhiteSpace(connectionName)) return;

        int currentIndex = block.IndexOf(currentStep);
        if (currentIndex <= 0) goto NotFound;

        for (int i = 0; i < currentIndex; i++)
        {
            var step = block[i];
            if (step.StepSetting.StepType != "IO.CanOpen") continue;

            try
            {
                var setting = MessagePackSerializer.Deserialize<CanOpenSetting>(
                    step.StepSetting.Setting, _opts);
                if (setting.ConnectionName == connectionName) return;
            }
            catch { }
        }

    NotFound:
        errors.Add(StepSettingError.Warning("CAN_LC01",
            $"在此步骤之前未找到针对连接 \"{connectionName}\" 的 CAN.Open 步骤"));
    }

    /// <summary>
    /// 检查当前 CyclicSendStop 之前是否存在匹配的 CyclicSendStart
    /// </summary>
    public static void CheckPrecedingCyclicStart(
        List<Step> block, Step currentStep, string connectionName, List<StepSettingError> errors)
    {
        if (string.IsNullOrWhiteSpace(connectionName)) return;

        int currentIndex = block.IndexOf(currentStep);
        if (currentIndex <= 0) goto NotFound;

        for (int i = 0; i < currentIndex; i++)
        {
            var step = block[i];
            if (step.StepSetting.StepType != "IO.CanCyclicSendStart") continue;

            try
            {
                var setting = MessagePackSerializer.Deserialize<CanCyclicSendStartSetting>(
                    step.StepSetting.Setting, _opts);
                if (setting.ConnectionName == connectionName) return;
            }
            catch { }
        }

    NotFound:
        errors.Add(StepSettingError.Warning("CAN_LC02",
            $"在此步骤之前未找到针对连接 \"{connectionName}\" 的 CAN.CyclicSendStart 步骤"));
    }
}
