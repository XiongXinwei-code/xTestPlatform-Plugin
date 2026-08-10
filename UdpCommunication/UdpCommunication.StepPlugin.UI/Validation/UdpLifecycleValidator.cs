using MessagePack;
using UdpCommunication.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace UdpCommunication.UI.Validation;

internal static class UdpLifecycleValidator
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

    /// <summary>
    /// 检查当前步骤之前是否存在匹配的 UDP_Open 步骤。
    /// </summary>
    /// <param name="openStepAddress">步骤引用的 Open 步骤地址。</param>
    public static void CheckPrecedingOpen(
        SequenceFile sequenceFile, List<Step> block, Step currentStep,
        string openStepAddress, List<StepSettingError> errors)
    {
        if (string.IsNullOrWhiteSpace(openStepAddress)) return;

        foreach (var step in GetPrecedingSteps(sequenceFile, block, currentStep))
        {
            if (step.StepSetting.StepType != UdpOpenPlugin.StepTypeIdConst) continue;
            if (step.StepSetting.StepAddress == openStepAddress) return;
        }

        errors.Add(StepSettingError.Warning("UDP_LC01",
            $"在此步骤之前未找到步骤地址 {openStepAddress} 对应的 UDP_Open 步骤"));
    }
}
