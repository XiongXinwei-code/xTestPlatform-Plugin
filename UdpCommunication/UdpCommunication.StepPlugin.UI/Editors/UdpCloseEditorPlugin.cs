using System.Windows;
using StepEditor.Abstractions;
using UdpCommunication.Models;
using UdpCommunication.UI.Views;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace UdpCommunication.UI.Editors;

public sealed class UdpCloseEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "Communication.UdpClose";
    public string IconPath => "pack://application:,,,/UdpCommunication.StepPlugin.UI;component/Resources/Icons/udp.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new UdpCloseEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new UdpClosePlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        StepEditorValidationContext context, CancellationToken cancellationToken = default)
    {
        var errors = new List<StepSettingError>();
        var serializer = new UdpClosePlugin().CreateSerializer();
        var s = context.Setting is { Length: > 0 }
            ? (UdpCloseSetting)serializer.Deserialize(context.Setting, 1)
            : (UdpCloseSetting)serializer.CreateDefault();

        UdpOpenStepAddressValidator.ValidateOpenStepAddress(
            s.OpenStepAddress, context.SequenceFile, errors);

        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
