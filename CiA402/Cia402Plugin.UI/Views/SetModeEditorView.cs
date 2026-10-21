using CiA402.UI.ViewModels;

namespace CiA402.UI.Views;

public sealed class SetModeEditorView : AxisEditorView
{
    public SetModeEditorView() : base(new SetModeViewModel()) { }
}
