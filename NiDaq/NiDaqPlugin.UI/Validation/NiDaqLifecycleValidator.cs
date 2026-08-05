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
    /// 将所属序列的 Setup → Main → Cleanup 三个区块按执行顺序合并为平铺列表，
    /// 返回 currentStep 之前的所有步骤，用于生命周期校验。
    /// </summary>
    private static IEnumerable<Step> GetPrecedingSteps(
        SequenceFile sequenceFile, List<Step> block, Step currentStep)
    {
        // 找到拥有当前 block 的 Sequence
        Sequence? ownerSeq = null;
        foreach (var seq in sequenceFile.Sequences.Values)
        {
            if (seq.StepItems.Values.Any(b => ReferenceEquals(b, block)))
            {
                ownerSeq = seq;
                break;
            }
        }
        if (ownerSeq == null) return [];

        // 按 Setup → Main → Cleanup 顺序合并所有区块
        var allSteps = new List<Step>();
        foreach (var blockType in new[] { BlockType.Setup, BlockType.Main, BlockType.Cleanup })
        {
            if (ownerSeq.StepItems.TryGetValue(blockType, out var b))
                allSteps.AddRange(b);
        }

        int currentIndex = allSteps.IndexOf(currentStep);
        return currentIndex > 0 ? allSteps.Take(currentIndex) : [];
    }

    /// <summary>
    /// 检查当前步骤之前是否存在匹配 TaskName 的 Config 步骤（AiConfig / SyncConfig / EncoderConfig）
    /// </summary>
    public static void CheckPrecedingConfig(
        SequenceFile sequenceFile, List<Step> block, Step currentStep, string taskName, List<StepSettingError> errors)
    {
        if (string.IsNullOrWhiteSpace(taskName)) return;

        foreach (var step in GetPrecedingSteps(sequenceFile, block, currentStep))
        {
            if (!_configStepTypes.Contains(step.StepSetting.StepType)) continue;
            try
            {
                string? name = GetTaskName(step);
                if (name == taskName) return;
            }
            catch { }
        }

        errors.Add(StepSettingError.Warning("DAQ_LC01",
            $"在此步骤之前未找到针对任务 \"{taskName}\" 的 NiDaq Config 步骤（AiConfig/SyncConfig/EncoderConfig）"));
    }

    /// <summary>
    /// 检查 Read 步骤之前是否有对应的 TaskStart
    /// </summary>
    public static void CheckPrecedingTaskStart(
        SequenceFile sequenceFile, List<Step> block, Step currentStep, string taskName, List<StepSettingError> errors)
    {
        if (string.IsNullOrWhiteSpace(taskName)) return;

        foreach (var step in GetPrecedingSteps(sequenceFile, block, currentStep))
        {
            if (step.StepSetting.StepType != "NiDaq.TaskStart") continue;
            try
            {
                var setting = MessagePackSerializer.Deserialize<NiDaqTaskStartSetting>(
                    step.StepSetting.Setting, _opts);
                if (setting.TaskName == taskName) return;
            }
            catch { }
        }

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
