using System.Windows;
using System.Windows.Controls;
using StepEditor.Abstractions;
using UdpCommunication.StepPlugin.UI.ViewModels;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace UdpCommunication.StepPlugin.UI.Views;

public partial class UdpEditorView : UserControl, IRefreshableEditor
{
    public UdpEditorViewModel ViewModel { get; }
    public UdpEditorView(IStepSettingSerializer serializer, Func<byte[], string> generateDescription, bool receive)
    {
        InitializeComponent();
        ViewModel = new UdpEditorViewModel(serializer, generateDescription, receive);
        DataContext = ViewModel;
    }
    public void RefreshFromStep(Step step) => ViewModel.AttachStep(step);
}
