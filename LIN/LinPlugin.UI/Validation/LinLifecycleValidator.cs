using LIN.Models;
using MessagePack;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace LIN.UI.Validation;

internal static class LinLifecycleValidator
{
    private static readonly MessagePackSerializerOptions _opts =
        MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);

    private static IEnumerable<Step> GetPrecedingSteps(
        SequenceFile sequenceFile, List<Step> block, Step currentStep)
    {
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

        var allSteps = new List<Step>();
        foreach (var blockType in new[] { BlockType.Setup, BlockType.Main, BlockType.Cleanup })
        {
            if (ownerSeq.StepItems.TryGetValue(blockType, out var b))
                allSteps.AddRange(b);
        }

        int currentIndex = allSteps.IndexOf(currentStep);
        return currentIndex > 0 ? allSteps.Take(currentIndex) : [];
    }

    /// <summary>检查当前步骤之前是否存在匹配的 LIN.Open 步骤</summary>
    public static void CheckPrecedingOpen(
        SequenceFile sequenceFile, List<Step> block, Step currentStep,
        string connectionName, List<StepSettingError> errors)
    {
        if (string.IsNullOrWhiteSpace(connectionName)) return;

        foreach (var step in GetPrecedingSteps(sequenceFile, block, currentStep))
        {
            if (step.StepSetting.StepType != "IO.LinOpen") continue;
            try
            {
                var s = MessagePackSerializer.Deserialize<LinOpenSetting>(step.StepSetting.Setting, _opts);
                if (s.ConnectionName == connectionName) return;
            }
            catch { }
        }

        errors.Add(StepSettingError.Warning("LIN_LC01",
            $"在此步骤之前未找到针对连接 \"{connectionName}\" 的 LIN_Open 步骤"));
    }

    /// <summary>检查当前 CyclicSendStop 之前是否存在匹配的 CyclicSendStart</summary>
    public static void CheckPrecedingCyclicStart(
        SequenceFile sequenceFile, List<Step> block, Step currentStep,
        string taskName, List<StepSettingError> errors)
    {
        if (string.IsNullOrWhiteSpace(taskName)) return;

        foreach (var step in GetPrecedingSteps(sequenceFile, block, currentStep))
        {
            if (step.StepSetting.StepType != "IO.LinCyclicSendStart") continue;
            try
            {
                var s = MessagePackSerializer.Deserialize<LinCyclicSendStartSetting>(step.StepSetting.Setting, _opts);
                if (s.TaskName == taskName) return;
            }
            catch { }
        }

        errors.Add(StepSettingError.Warning("LIN_LC02",
            $"在此步骤之前未找到针对任务 \"{taskName}\" 的 LIN_CyclicSendStart 步骤"));
    }
}
