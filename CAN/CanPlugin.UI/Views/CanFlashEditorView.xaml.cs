using System.Windows;
using System.Windows.Controls;
using CAN.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI.Views;

public partial class CanFlashEditorView : UserControl, IRefreshableEditor
{
    public CanFlashViewModel ViewModel { get; }
    public Action<string, Action>? ExecuteCommand { get; set; }

    public static readonly DependencyProperty SequenceFileProperty =
        DependencyProperty.Register(nameof(SequenceFile), typeof(SequenceFile), typeof(CanFlashEditorView),
            new PropertyMetadata(null));
    public SequenceFile? SequenceFile { get => (SequenceFile?)GetValue(SequenceFileProperty); set => SetValue(SequenceFileProperty, value); }

    public static readonly DependencyProperty EditPositionProperty =
        DependencyProperty.Register(nameof(EditPosition), typeof(EditPosition), typeof(CanFlashEditorView),
            new PropertyMetadata(null));
    public EditPosition? EditPosition { get => (EditPosition?)GetValue(EditPositionProperty); set => SetValue(EditPositionProperty, value); }

    public CanFlashEditorView()
    {
        InitializeComponent();
        ViewModel = new CanFlashViewModel();
        DataContext = ViewModel;
    }

    public void RefreshFromStep(Step step)
    {
        ViewModel.AttachSerializer(new CAN.Flash.CanFlashPlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }

    private void OnBrowseFile(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择固件文件",
            Filter = "固件文件|*.hex;*.s19;*.srec;*.mot;*.bin|Intel HEX|*.hex|S-Record|*.s19;*.srec;*.mot|二进制文件|*.bin|所有文件|*.*"
        };

        if (dialog.ShowDialog() == true)
            ViewModel.FilePath = $"\"{dialog.FileName}\"";
    }

    private async void OnAnalyzeFirmware(object sender, RoutedEventArgs e)
    {
        await ViewModel.AnalyzeFirmwareAsync();
    }

    private void OnApplySuggestion(object sender, RoutedEventArgs e)
    {
        ViewModel.ApplySuggestedAlfid();
    }

    private void OnApplyPreset(object sender, RoutedEventArgs e)
    {
        ViewModel.ApplySelectedPreset();
    }

    private void OnDeletePreset(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedPreset is not { } preset)
            return;

        var confirm = MessageBox.Show($"确定删除预设「{preset.Name}」吗？", "删除预设",
            MessageBoxButton.OKCancel, MessageBoxImage.Question);

        if (confirm == MessageBoxResult.OK)
            ViewModel.DeleteSelectedPreset();
    }

    private void OnSavePreset(object sender, RoutedEventArgs e)
    {
        var dialog = new PresetNameDialog
        {
            Owner = Window.GetWindow(this),
            PresetName = ViewModel.SelectedPreset?.Name ?? string.Empty,
            Remark = ViewModel.SelectedPreset?.Remark ?? string.Empty
        };

        if (dialog.ShowDialog() == true)
            ViewModel.SaveAsPreset(dialog.PresetName, dialog.Remark);
    }
}
