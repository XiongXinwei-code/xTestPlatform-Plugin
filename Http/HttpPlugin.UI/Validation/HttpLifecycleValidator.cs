using Http.Models;
using MessagePack;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace Http.UI.Validation;

/// <summary>
/// HTTP 客户端生命周期校验，检查请求步骤之前是否存在同名客户端的创建步骤
/// </summary>
internal static class HttpLifecycleValidator
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

    public static void CheckPrecedingCreate(
        SequenceFile sequenceFile, List<Step> block, Step currentStep, string clientName, List<StepSettingError> errors)
    {
        if (string.IsNullOrWhiteSpace(clientName)) return;

        foreach (var step in GetPrecedingSteps(sequenceFile, block, currentStep))
        {
            if (step.StepSetting.StepType != "IO.HttpClientCreate") continue;
            try
            {
                var setting = MessagePackSerializer.Deserialize<HttpClientCreateSetting>(
                    step.StepSetting.Setting, _opts);
                if (setting.ClientName == clientName) return;
            }
            catch { }
        }

        errors.Add(StepSettingError.Warning("HTTP_LC01",
            $"在此步骤之前未找到针对客户端 \"{clientName}\" 的 Http_ClientCreate 步骤"));
    }
}
