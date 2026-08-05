using MessagePack;
using SerialPort.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace SerialPort.UI.Validation;

internal static class SerialPortLifecycleValidator
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
    /// 检查当前步骤之前是否存在匹配的 SerialPort.Open 步骤
    /// </summary>
    public static void CheckPrecedingOpen(
        SequenceFile sequenceFile, List<Step> block, Step currentStep, string portName, List<StepSettingError> errors)
    {
        if (string.IsNullOrWhiteSpace(portName)) return;

        foreach (var step in GetPrecedingSteps(sequenceFile, block, currentStep))
        {
            if (step.StepSetting.StepType != "IO.SerialPortOpen") continue;
            try
            {
                var setting = MessagePackSerializer.Deserialize<SerialPortOpenSetting>(
                    step.StepSetting.Setting, _opts);
                if (setting.PortName == portName) return;
            }
            catch { }
        }

        errors.Add(StepSettingError.Warning("SP_LC01",
            $"在此步骤之前未找到针对端口 \"{portName}\" 的 SerialPort.Open 步骤"));
    }
}
