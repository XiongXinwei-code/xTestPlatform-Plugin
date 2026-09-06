using System.Windows;
using Ethernet.DoIP;
using Ethernet.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace Ethernet.UI.Editors;

public sealed class DoipVehicleDiscoveryEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "DoIP.VehicleDiscovery";
    public string IconPath   => "pack://application:,,,/Ethernet.StepPlugin.UI;component/Resources/Icons/ethernet.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new DoipVehicleDiscoveryEditorView();
        view.SequenceFile = sequenceFile;
        view.RefreshFromStep(step);
        return view;
    }
}
