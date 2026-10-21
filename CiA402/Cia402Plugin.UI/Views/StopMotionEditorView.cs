using CiA402.UI.ViewModels;

namespace CiA402.UI.Views;

public sealed class StopMotionEditorView : AxisEditorView
{
    public StopMotionEditorView() : base(new StopMotionViewModel()) { }
}
