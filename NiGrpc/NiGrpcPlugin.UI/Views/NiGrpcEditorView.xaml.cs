using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using NiGrpc.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace NiGrpc.UI.Views;

public partial class NiGrpcEditorView : UserControl, IRefreshableEditor
{
    private readonly INiGrpcPlugin _plugin;
    public NiGrpcViewModel ViewModel { get; } = new();
    public Action<string, Action>? ExecuteCommand { get => ViewModel.ExecuteCommand; set => ViewModel.ExecuteCommand = value; }
    public static readonly DependencyProperty SequenceFileProperty = DependencyProperty.Register(nameof(SequenceFile), typeof(SequenceFile), typeof(NiGrpcEditorView));
    public static readonly DependencyProperty EditPositionProperty = DependencyProperty.Register(nameof(EditPosition), typeof(EditPosition), typeof(NiGrpcEditorView));
    public SequenceFile? SequenceFile { get => (SequenceFile?)GetValue(SequenceFileProperty); set => SetValue(SequenceFileProperty, value); }
    public EditPosition? EditPosition { get => (EditPosition?)GetValue(EditPositionProperty); set => SetValue(EditPositionProperty, value); }

    public NiGrpcEditorView(INiGrpcPlugin plugin)
    {
        _plugin = plugin; InitializeComponent(); DataContext = ViewModel;
        MainTab.Header = ((IStepPlugin)plugin).DisplayName.Split('_')[^1];
        MainTab.Image = new BitmapImage(new Uri(plugin.IconPath, UriKind.Absolute));
        EndpointRow.Visibility = VisibleFor("NiGrpc.Connect");
        bool isOpen = plugin.StepTypeId is "NiGrpc.DaqOpen" or "NiGrpc.XnetOpen";
        bool isInvoke = (plugin.StepTypeId.StartsWith("NiGrpc.Daq", StringComparison.Ordinal) || plugin.StepTypeId.StartsWith("NiGrpc.Xnet", StringComparison.Ordinal)) && !isOpen;
        SessionRow.Visibility = isOpen || isInvoke || plugin.StepTypeId == "NiGrpc.CloseSession" ? Visibility.Visible : Visibility.Collapsed;
        ConfigurationRow.Visibility = VisibleFor("NiGrpc.XnetOpen");
        RequestRow.Visibility = ResultRow.Visibility = isInvoke ? Visibility.Visible : Visibility.Collapsed;
        PluginCommon.UI.PluginEditorCommit.Attach(this, ViewModel.FlushPendingSave);
        Unloaded += (_, _) => ViewModel.FlushPendingSave();
    }
    private Visibility VisibleFor(params string[] ids) => ids.Contains(_plugin.StepTypeId, StringComparer.Ordinal) ? Visibility.Visible : Visibility.Collapsed;
    public void RefreshFromStep(Step step) => ViewModel.Attach(step, _plugin.CreateSerializer());
}
