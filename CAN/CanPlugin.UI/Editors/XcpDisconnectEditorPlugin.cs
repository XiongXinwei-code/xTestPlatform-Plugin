using System.Windows;
using CAN.UI.Views;
using CAN.XCP;
using CAN.XCP.Models;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI;

public sealed class XcpDisconnectEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "XCP.Disconnect";
    public string IconPath   => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new XcpDisconnectEditorView();
        view.SequenceFile = sequenceFile;
        view.RefreshFromStep(step);
        return view;
    }
}
