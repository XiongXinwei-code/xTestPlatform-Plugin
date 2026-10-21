using CiA402.UI.ViewModels;

namespace CiA402.UI.Views;

public sealed class RunTorqueEditorView : AxisEditorView
{
    public RunTorqueEditorView() : base(new RunTorqueViewModel()) { }
}
