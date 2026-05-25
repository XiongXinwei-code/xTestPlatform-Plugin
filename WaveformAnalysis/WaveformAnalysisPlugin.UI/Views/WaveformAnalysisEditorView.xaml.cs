using System.Windows.Controls;
using WaveformAnalysisPlugin.UI.ViewModels;
using xTestPlatform.Core.SequenceModels;

namespace WaveformAnalysisPlugin.UI.Views
{
    public partial class WaveformAnalysisEditorView : UserControl
    {
        public WaveformAnalysisEditorView(Step step, SequenceFile? sequenceFile)
        {
            InitializeComponent();
            DataContext = new WaveformAnalysisEditorViewModel(step);
        }
    }
}

