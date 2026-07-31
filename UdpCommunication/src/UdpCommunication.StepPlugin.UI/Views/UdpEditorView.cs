using System.Windows;
using System.Windows.Controls;
using UdpCommunication.StepPlugin.Models;
using UdpCommunication.StepPlugin.Protocol;
using xTestPlatform.Core.SequenceModels;
using xTestPlatform.Core.Plugins.Contracts;

namespace UdpCommunication.StepPlugin.UI.Views;

public sealed class UdpEditorView : UserControl
{
    private readonly Step _step;
    private readonly IStepSettingSerializer _serializer;
    private readonly UdpSendSetting _setting;

    public UdpEditorView(Step step, IStepSettingSerializer serializer, bool receive)
    {
        _step = step;
        _serializer = serializer;
        _setting = (UdpSendSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
        var panel = new StackPanel { Margin = new Thickness(16) };
        Content = new ScrollViewer { Content = panel };
        AddText(panel, "目标 IP", () => _setting.RemoteAddress, value => _setting.RemoteAddress = value);
        AddText(panel, "目标端口", () => _setting.RemotePort.ToString(), value => _setting.RemotePort = ParseInt(value));
        AddText(panel, "本地 IP", () => _setting.LocalAddress, value => _setting.LocalAddress = value);
        AddText(panel, "本地端口", () => _setting.LocalPort.ToString(), value => _setting.LocalPort = ParseInt(value));
        AddFormat(panel, "发送格式", () => _setting.RequestFormat, value => _setting.RequestFormat = value);
        AddText(panel, "发送报文", () => _setting.RequestData, value => _setting.RequestData = value);
        if (receive) AddReceiveFields(panel, (UdpSendAndReceiveSetting)_setting);
    }

    private void AddReceiveFields(Panel panel, UdpSendAndReceiveSetting setting)
    {
        AddText(panel, "接收超时(ms)", () => setting.ReceiveTimeoutMs.ToString(), value => setting.ReceiveTimeoutMs = ParseInt(value));
        AddFormat(panel, "回复格式", () => setting.ReplyFormat, value => setting.ReplyFormat = value);
        AddText(panel, "期望回复", () => setting.ExpectedReply, value => setting.ExpectedReply = value);
        AddText(panel, "匹配模式(Exact/Contains)", () => setting.MatchMode.ToString(), value => setting.MatchMode = Enum.TryParse<UdpReplyMatchMode>(value, true, out var mode) ? mode : UdpReplyMatchMode.Exact);
        AddText(panel, "回复变量", () => setting.ResponseVariable, value => setting.ResponseVariable = value);
    }

    private void AddText(Panel panel, string label, Func<string> get, Action<string> set)
    {
        panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 8, 0, 2) });
        var box = new TextBox { Text = get(), MinWidth = 260 };
        box.LostFocus += (_, _) => { set(box.Text); Save(); };
        panel.Children.Add(box);
    }
    private void AddFormat(Panel panel, string label, Func<UdpPacketFormat> get, Action<UdpPacketFormat> set)
    {
        panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 8, 0, 2) });
        var box = new ComboBox { ItemsSource = Enum.GetValues<UdpPacketFormat>(), SelectedItem = get(), MinWidth = 260 };
        box.SelectionChanged += (_, _) => { if (box.SelectedItem is UdpPacketFormat value) { set(value); Save(); } };
        panel.Children.Add(box);
    }
    private void Save() => _step.StepSetting.Setting = _serializer.Serialize(_setting);
    private static int ParseInt(string value) => int.TryParse(value, out var result) ? result : 0;
}
