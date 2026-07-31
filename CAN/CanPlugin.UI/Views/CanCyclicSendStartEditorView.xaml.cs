using System.Windows;
using System.Windows.Controls;
using CAN.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI.Views;

public partial class CanCyclicSendStartEditorView : UserControl, IRefreshableEditor
{
    public CanCyclicSendStartViewModel ViewModel { get; }
    public Action<string, Action>? ExecuteCommand { get; set; }
    public SequenceFile? SequenceFile { get; set; }
    public EditPosition? EditPosition { get; set; }

    public CanCyclicSendStartEditorView()
    {
        InitializeComponent();
        ViewModel = new CanCyclicSendStartViewModel();
        DataContext = ViewModel;
    }

    public void RefreshFromStep(Step step)
    {
        ViewModel.AttachSerializer(new CanCyclicSendStartPlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }

    private void OnAddMessage(object sender, RoutedEventArgs e)
    {
        ViewModel.AddMessage();
    }

    private void OnRemoveMessage(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Messages.Count > 0)
        {
            // 删除最后一个，或者选中的
            ViewModel.RemoveMessage(ViewModel.Messages[^1]);
        }
    }
}
