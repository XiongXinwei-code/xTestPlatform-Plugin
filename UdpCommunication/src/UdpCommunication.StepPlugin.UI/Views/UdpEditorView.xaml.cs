using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using StepEditor.Abstractions;
using UdpCommunication.StepPlugin.UI.ViewModels;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace UdpCommunication.StepPlugin.UI.Views;

public partial class UdpEditorView : UserControl, IRefreshableEditor
{
    public UdpEditorViewModel ViewModel { get; }
    public Action<string, Action>? ExecuteCommand
    {
        get => ViewModel.ExecuteCommand;
        set => ViewModel.ExecuteCommand = value;
    }

    public SequenceFile? SequenceFile { get; set; }
    public EditPosition? EditPosition { get; set; }

    public UdpEditorView(IStepSettingSerializer serializer, Func<byte[], string> generateDescription, bool receive)
    {
        InitializeComponent();
        ViewModel = new UdpEditorViewModel(serializer, generateDescription, receive);
        DataContext = ViewModel;
    }
    public void RefreshFromStep(Step step) => ViewModel.AttachStep(step);

    private void TextBox_OnLostFocus(object sender, RoutedEventArgs e) => CommitTextBox((TextBox)sender);

    private void TextBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        var textBox = (TextBox)sender;
        if (!textBox.AcceptsReturn)
        {
            CommitTextBox(textBox);
            e.Handled = true;
            return;
        }

        Dispatcher.BeginInvoke(DispatcherPriority.Background, () => CommitTextBox(textBox));
    }

    private void ComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e) =>
        Dispatcher.BeginInvoke(DispatcherPriority.Background, ViewModel.CommitPendingChanges);

    private void CommitTextBox(TextBox textBox)
    {
        ViewModel.CommitPendingChanges();
    }
}
