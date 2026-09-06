using System.Windows;
using Http.Models;
using Http.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace Http.UI;

public sealed class HttpClientCloseEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.HttpClientClose";
    public string IconPath => "pack://application:,,,/Http.StepPlugin.UI;component/Resources/Icons/http.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new HttpClientCloseEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new HttpClientClosePlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }
}
