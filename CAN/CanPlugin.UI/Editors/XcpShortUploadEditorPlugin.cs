using System.Windows;
using CAN.UI.Views;
using CAN.XCP;
using CAN.XCP.Models;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI;

public sealed class XcpShortUploadEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "XCP.ShortUpload";
    public string IconPath   => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new XcpShortUploadEditorView();
        view.SequenceFile = sequenceFile;
        view.RefreshFromStep(step);
        return view;
    }
}
