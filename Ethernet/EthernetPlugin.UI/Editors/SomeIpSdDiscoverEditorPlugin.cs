using System.Windows;
using Ethernet.SomeIP;
using Ethernet.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace Ethernet.UI.Editors;

public sealed class SomeIpSdDiscoverEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "SomeIp.SdDiscover";
    public string IconPath   => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/ethernet.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new SomeIpSdDiscoverEditorView();
        view.SequenceFile = sequenceFile;
        view.RefreshFromStep(step);
        return view;
    }
}
