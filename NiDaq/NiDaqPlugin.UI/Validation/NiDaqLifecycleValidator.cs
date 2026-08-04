using MessagePack;
using NiDaq.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace NiDaq.UI.Validation;

internal static class NiDaqLifecycleValidator
{
    private static readonly MessagePackSerializerOptions _opts =
        MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);

    private static readonly HashSet<string> _configStepTypes = new()
    {
        "NiDaq.AiConfig",
        "NiDaq.SyncConfig",
        "NiDaq.EncoderConfig"
    };

    /// <summary>
    /// 检查当前步骤之前是否存在匹配 TaskName 的 Config 步骤（AiConfig / SyncConfig / EncoderConfig）
    /// </summary>
    public static void CheckPrecedingConfig(
        List<Step> block, Step currentStep, string taskName, List<StepSettingError> errors)
    {
        if (string.IsNullOrWhiteSpace(taskName)) return;

        int currentIndex = block.IndexOf(currentStep);
        if (currentIndex <= 0) goto NotFound;

        for (int i = 0; i < currentIndex; i++)
        {
            var step = block[i];
            if (!_configStepTypes.Contains(step.StepSetting.StepType)) continue;

            try
            {
                string? name = GetTaskName(step);
                if (name == taskName) return;
            }
            catch { }
        }

    NotFound:
        errors.Add(StepSettingError.Warning("DAQ_LC01",
            $"在此步骤之前未找到针对任务 \"{taskName}\" 的 NiDaq Config 步骤（AiConfig/SyncConfig/EncoderConfig）"));
    }

    /// <summary>
    /// 检查 Read 步骤之前是否有对应的 TaskStart
    /// </summary>
    public static void CheckPrecedingTaskStart(
        List<Step> block, Step currentStep, string taskName, List<StepSettingError> errors)
    {
        if (string.IsNullOrWhiteSpace(taskName)) return;

        int currentIndex = block.IndexOf(currentStep);
        if (currentIndex <= 0) goto NotFound;

        for (int i = 0; i < currentIndex; i++)
        {
            var step = block[i];
            if (step.StepSetting.StepType != "NiDaq.TaskStart") continue;

            try
            {
                var setting = MessagePackSerializer.Deserialize<NiDaqTaskStartSetting>(
                    step.StepSetting.Setting, _opts);
                if (setting.TaskName == taskName) return;
            }
            catch { }
        }

    NotFound:
        errors.Add(StepSettingError.Warning("DAQ_LC02",
            $"在此步骤之前未找到针对任务 \"{taskName}\" 的 NiDaq.TaskStart 步骤"));
    }

    private static string? GetTaskName(Step step)
    {
        var data = step.StepSetting.Setting;
        if (data is not { Length: > 0 }) return null;

        return step.StepSetting.StepType switch
        {
            "NiDaq.AiConfig" => MessagePackSerializer.Deserialize<NiDaqAiConfigSetting>(data, _opts).TaskName,
            "NiDaq.SyncConfig" => MessagePackSerializer.Deserialize<NiDaqSyncConfigSetting>(data, _opts).TaskName,
            "NiDaq.EncoderConfig" => MessagePackSerializer.Deserialize<NiDaqEncoderConfigSetting>(data, _opts).TaskName,
            _ => null
        };
    }
}
