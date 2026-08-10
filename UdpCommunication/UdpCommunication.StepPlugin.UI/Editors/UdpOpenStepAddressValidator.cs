using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace UdpCommunication.UI.Editors;

/// <summary>
/// 校验此步骤的 OpenStepAddress 是否指向当前 TestPlan 中的某个 UDP_Open 步骤。
/// </summary>
internal static class UdpOpenStepAddressValidator
{
    public static void ValidateOpenStepAddress(
        string openStepAddress,
        SequenceFile? sequenceFile,
        List<StepSettingError> errors,
        string errorCodeIfEmpty = "UDP_010",
        string errorCodeIfUnknown = "UDP_011")
    {
        if (string.IsNullOrWhiteSpace(openStepAddress))
        {
            errors.Add(StepSettingError.Error(errorCodeIfEmpty,
                "未选择 UDP_Open 步骤：请先在 TestPlan 中创建 UDP_Open 步骤，然后在此下拉框中选择"));
            return;
        }

        if (sequenceFile == null)
        {
            errors.Add(StepSettingError.Error(errorCodeIfUnknown,
                "无法获取当前 TestPlan（SequenceFile 为空）"));
            return;
        }

        foreach (var seq in sequenceFile.Sequences.Values)
        {
            foreach (var block in seq.StepItems.Values)
            {
                foreach (var step in block)
                {
                    if (step.StepSetting?.StepType == UdpOpenPlugin.StepTypeIdConst
                        && step.StepSetting.StepAddress == openStepAddress)
                    {
                        return;
                    }
                }
            }
        }

        errors.Add(StepSettingError.Error(errorCodeIfUnknown,
            $"步骤地址 {openStepAddress} 在当前 TestPlan 中不存在 UDP_Open 步骤"));
    }
}
