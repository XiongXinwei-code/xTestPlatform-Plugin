using CiA402.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace CiA402.Core;

public static class AxisSettingSerializers
{
    public static IStepSettingSerializer For(AxisSetting setting) => setting switch
    {
        BindAxisSetting => new BindAxisPlugin().CreateSerializer(),
        DisableSetting => new DisablePlugin().CreateSerializer(),
        EnableSetting => new EnablePlugin().CreateSerializer(),
        HomeSetting => new HomePlugin().CreateSerializer(),
        MovePositionSetting => new MovePositionPlugin().CreateSerializer(),
        ReadDiagnosticsSetting => new ReadDiagnosticsPlugin().CreateSerializer(),
        ReadFeedbackSetting => new ReadFeedbackPlugin().CreateSerializer(),
        ReadModeSetting => new ReadModePlugin().CreateSerializer(),
        ReadStateSetting => new ReadStatePlugin().CreateSerializer(),
        ResetFaultSetting => new ResetFaultPlugin().CreateSerializer(),
        RunTorqueSetting => new RunTorquePlugin().CreateSerializer(),
        RunVelocitySetting => new RunVelocityPlugin().CreateSerializer(),
        SetModeSetting => new SetModePlugin().CreateSerializer(),
        StopMotionSetting => new StopMotionPlugin().CreateSerializer(),
        UnbindAxisSetting => new UnbindAxisPlugin().CreateSerializer(),
        WriteCspTargetSetting => new WriteCspTargetPlugin().CreateSerializer(),
        WriteCstTargetSetting => new WriteCstTargetPlugin().CreateSerializer(),
        WriteCsvTargetSetting => new WriteCsvTargetPlugin().CreateSerializer(),
        _ => throw new ArgumentException("未知的 CiA402 设置类型")
    };

    public static IStepSettingSerializer ForId(string id) => id switch
    {
        "Motion.CiA402BindAxis" => new BindAxisPlugin().CreateSerializer(),
        "Motion.CiA402Disable" => new DisablePlugin().CreateSerializer(),
        "Motion.CiA402Enable" => new EnablePlugin().CreateSerializer(),
        "Motion.CiA402Home" => new HomePlugin().CreateSerializer(),
        "Motion.CiA402MovePosition" => new MovePositionPlugin().CreateSerializer(),
        "Motion.CiA402ReadDiagnostics" => new ReadDiagnosticsPlugin().CreateSerializer(),
        "Motion.CiA402ReadFeedback" => new ReadFeedbackPlugin().CreateSerializer(),
        "Motion.CiA402ReadMode" => new ReadModePlugin().CreateSerializer(),
        "Motion.CiA402ReadState" => new ReadStatePlugin().CreateSerializer(),
        "Motion.CiA402ResetFault" => new ResetFaultPlugin().CreateSerializer(),
        "Motion.CiA402RunTorque" => new RunTorquePlugin().CreateSerializer(),
        "Motion.CiA402RunVelocity" => new RunVelocityPlugin().CreateSerializer(),
        "Motion.CiA402SetMode" => new SetModePlugin().CreateSerializer(),
        "Motion.CiA402StopMotion" => new StopMotionPlugin().CreateSerializer(),
        "Motion.CiA402UnbindAxis" => new UnbindAxisPlugin().CreateSerializer(),
        "Motion.CiA402WriteCspTarget" => new WriteCspTargetPlugin().CreateSerializer(),
        "Motion.CiA402WriteCstTarget" => new WriteCstTargetPlugin().CreateSerializer(),
        "Motion.CiA402WriteCsvTarget" => new WriteCsvTargetPlugin().CreateSerializer(),
        _ => throw new ArgumentException($"未知的 CiA402 步骤：{id}")
    };
}
