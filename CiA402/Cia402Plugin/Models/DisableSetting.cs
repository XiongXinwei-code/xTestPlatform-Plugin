using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace CiA402.Models;

/// <summary>
/// 释放使能设置，无额外专属参数。
/// 继承 <see cref="AxisSetting.AxisName"/>（轴名称表达式）、
/// <see cref="AxisSetting.TimeoutMs"/>（步骤总超时，毫秒）和
/// <see cref="AxisSetting.PollIntervalMs"/>（状态轮询间隔，毫秒）。
/// </summary>
[MessagePackObject(true)]
public sealed class DisableSetting : AxisSetting
{
    
}
