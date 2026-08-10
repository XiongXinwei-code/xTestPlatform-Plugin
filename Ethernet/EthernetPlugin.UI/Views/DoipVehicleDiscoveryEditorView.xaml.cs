using System.Windows;
using System.Windows.Controls;
using Ethernet.DoIP;
using Ethernet.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace Ethernet.UI.Views;

public partial class DoipVehicleDiscoveryEditorView : UserControl, IRefreshableEditor
{
    public DoipVehicleDiscoveryViewModel ViewModel { get; }
    public Action<string, Action>? ExecuteCommand { get; set; }

    public static readonly DependencyProperty SequenceFileProperty =
        DependencyProperty.Register(nameof(SequenceFile), typeof(SequenceFile), typeof(DoipVehicleDiscoveryEditorView),
            new PropertyMetadata(null));
    public SequenceFile? SequenceFile
    {
        get => (SequenceFile?)GetValue(SequenceFileProperty);
        set => SetValue(SequenceFileProperty, value);
    }

    public static readonly DependencyProperty EditPositionProperty =
        DependencyProperty.Register(nameof(EditPosition), typeof(EditPosition), typeof(DoipVehicleDiscoveryEditorView),
            new PropertyMetadata(null));
    public EditPosition? EditPosition
    {
        get => (EditPosition?)GetValue(EditPositionProperty);
        set => SetValue(EditPositionProperty, value);
    }

    public DoipVehicleDiscoveryEditorView()
    {
        InitializeComponent();
        ViewModel   = new DoipVehicleDiscoveryViewModel();
        DataContext = ViewModel;
    }

    public void RefreshFromStep(Step step)
    {
        ViewModel.AttachSerializer(new DoipVehicleDiscoveryPlugin().CreateSerializer());
        ViewModel.AttachStep(step);
    }
}
