using System.Windows;
using System.Windows.Controls;
using OpcUa.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace OpcUa.UI.Views;

public partial class OpcUaConnectEditorView : UserControl, IRefreshableEditor
{
    private bool _syncingPassword;
    public OpcUaConnectViewModel ViewModel { get; }
    public Action<string, Action>? ExecuteCommand { get; set; }
    public static readonly DependencyProperty SequenceFileProperty =
        DependencyProperty.Register(nameof(SequenceFile), typeof(SequenceFile), typeof(OpcUaConnectEditorView),
            new PropertyMetadata(null));
    public SequenceFile? SequenceFile { get => (SequenceFile?)GetValue(SequenceFileProperty); set => SetValue(SequenceFileProperty, value); }
    public static readonly DependencyProperty EditPositionProperty =
        DependencyProperty.Register(nameof(EditPosition), typeof(EditPosition), typeof(OpcUaConnectEditorView),
            new PropertyMetadata(null));
    public EditPosition? EditPosition { get => (EditPosition?)GetValue(EditPositionProperty); set => SetValue(EditPositionProperty, value); }

    public OpcUaConnectEditorView()
    {
        InitializeComponent();
        ViewModel = new OpcUaConnectViewModel();
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(ViewModel.Password))
                SyncPasswordFromViewModel();
        };
        DataContext = ViewModel;
        Unloaded += (_, _) => ViewModel.FlushPendingSave();
    }

    public void RefreshFromStep(Step step)
    {
        ViewModel.AttachSerializer(new OpcUaConnectPlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }

    private void PasswordInput_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (!_syncingPassword && ViewModel != null)
            ViewModel.Password = PasswordInput.Password;
    }

    private void SyncPasswordFromViewModel()
    {
        if (PasswordInput.Password == ViewModel.Password) return;
        _syncingPassword = true;
        try { PasswordInput.Password = ViewModel.Password; }
        finally { _syncingPassword = false; }
    }
}
