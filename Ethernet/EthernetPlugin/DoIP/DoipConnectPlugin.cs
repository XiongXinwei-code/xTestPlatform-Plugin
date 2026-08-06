using Ethernet.DoIP.Executors;
using Ethernet.DoIP.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Ethernet.DoIP;

public sealed class DoipConnectPlugin : StepPluginBase<DoipConnectSetting>
{
    public override string StepTypeId  => "DoIP.Connect";
    public override string DisplayName => "DoIP_Connect";
    public override string Category    => "Communication";
    public override string IconPath    => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/ethernet.png";

    public override string Description =>
        "建立 DoIP（ISO 13400）TCP 连接并执行路由激活，以 SessionName 注册会话供后续步骤使用。" +
        "Setting 字段：SessionName(string,表达式,会话标识名,默认\"DOIP1\"), " +
        "RemoteHost(string,表达式,DoIP实体IP,默认\"192.168.1.10\"), " +
        "RemotePort(string,表达式,TCP端口,默认\"13400\"), " +
        "SourceAddress(string,表达式,诊断仪逻辑地址,默认\"0x0E00\"), " +
        "ActivationType(枚举,激活类型:Default/WwhObd/CentralSecurity,默认Default), " +
        "TimeoutMs(int,超时毫秒,默认3000), " +
        "EnableLog(bool,是否输出日志,默认true)。";

    public override IStepExecutor CreateExecutor() => new DoipConnectExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"DoIP Connect: {s.SessionName} -> {s.RemoteHost}:{s.RemotePort} SA={s.SourceAddress}";
    }
}
