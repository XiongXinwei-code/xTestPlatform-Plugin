using MessagePack;
using OpcUa.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace OpcUa.UI.Validation;

internal static class OpcUaLifecycleValidator
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

    public static void CheckPrecedingConnect(
        SequenceFile sequenceFile, List<Step> block, Step currentStep, string connectionName, List<StepSettingError> errors)
    {
        if (string.IsNullOrWhiteSpace(connectionName)) return;

        foreach (var step in GetPrecedingSteps(sequenceFile, block, currentStep))
        {
            if (step.StepSetting.StepType != "OpcUa.Connect") continue;
            try
            {
                var setting = MessagePackSerializer.Deserialize<OpcUaConnectSetting>(
                    step.StepSetting.Setting, _opts);
                if (setting.ConnectionName == connectionName) return;
            }
            catch { }
        }

        errors.Add(StepSettingError.Warning("OPCUA_LC01",
            $"在此步骤之前未找到针对连接 \"{connectionName}\" 的 OpcUA.Connect 步骤"));
    }
}
