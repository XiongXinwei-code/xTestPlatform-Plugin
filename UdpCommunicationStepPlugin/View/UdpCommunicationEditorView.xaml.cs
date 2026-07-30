using System.Windows;
using System.Windows.Controls;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;
using UdpCommunicationStepPlugin.ViewModels;

namespace UdpCommunicationStepPlugin.View;

public partial class UdpCommunicationEditorView : UserControl, IRefreshableEditor
{
    public UdpCommunicationEditorViewModel ViewModel { get; } = new();
    public UdpCommunicationEditorView() { InitializeComponent(); DataContext = ViewModel; }
    public void RefreshFromStep(Step step) { ViewModel.AttachSerializer(new UdpCommunicationPlugin().CreateSerializer()); ViewModel.AttachStep(step); }
    private void Save_OnLostFocus(object sender, RoutedEventArgs eventArgs) => ViewModel.Save();
}
