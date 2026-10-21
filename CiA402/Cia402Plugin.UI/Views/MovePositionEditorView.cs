using CiA402.UI.ViewModels;

namespace CiA402.UI.Views;

public sealed class MovePositionEditorView : AxisEditorView
{
    public MovePositionEditorView() : base(new MovePositionViewModel()) { }
}
