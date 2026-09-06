using System.Windows;
using System.Xml.XPath;
using Http.Models;
using Http.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace Http.UI;

public sealed class HttpXmlExtractEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.HttpXmlExtract";
    public string IconPath => "pack://application:,,,/Http.StepPlugin.UI;component/Resources/Icons/http.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new HttpXmlExtractEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new HttpXmlExtractPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }
}
