using System.Net;
using System.Windows;
using StepEditor.Abstractions;
using UdpCommunication.Models;
using UdpCommunication.UI.Views;
using UdpCommunication.Validation;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace UdpCommunication.UI.Editors;

public sealed class UdpOpenEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => UdpOpenPlugin.StepTypeIdConst;
    public string IconPath => "pack://application:,,,/UdpCommunication.StepPlugin.UI;component/Resources/Icons/udp.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new UdpOpenEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new UdpOpenPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        StepEditorValidationContext context, CancellationToken cancellationToken = default)
    {
        var errors = new List<StepSettingError>();
        var serializer = new UdpOpenPlugin().CreateSerializer();
        var s = context.Setting is { Length: > 0 }
            ? (UdpOpenSetting)serializer.Deserialize(context.Setting, 1)
            : (UdpOpenSetting)serializer.CreateDefault();

        if (!IPAddress.TryParse(s.LocalAddress.Trim('"'), out _))
        {
            errors.Add(StepSettingError.Error("UDP_001", "本地地址不是有效的 IP 地址"));
        }
        else if (!context.Evaluator.ValidateExpression(s.LocalAddress, context.ExecutionContext, out var localErr))
        {
            errors.Add(StepSettingError.Error("UDP_001E", $"LocalAddress 表达式无效：{localErr}"));
        }

        if (s.LocalPort is < 1 or > 65535)
        {
            errors.Add(StepSettingError.Error("UDP_007",
                "本地端口必须在 1~65535 之间（不允许 0，否则后续步骤无法引用）"));
        }

        if (!IPAddress.TryParse(s.DefaultRemoteAddress.Trim('"'), out _))
        {
            errors.Add(StepSettingError.Warning("UDP_002", "默认目标地址不是有效的 IP 地址"));
        }
        else if (!context.Evaluator.ValidateExpression(s.DefaultRemoteAddress, context.ExecutionContext, out var remoteErr))
        {
            errors.Add(StepSettingError.Warning("UDP_002E", $"DefaultRemoteAddress 表达式无效：{remoteErr}"));
        }

        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
