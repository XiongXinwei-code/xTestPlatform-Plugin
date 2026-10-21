using CiA402.UI.ViewModels;

namespace CiA402.UI.Views;

public sealed class EnableEditorView : AxisEditorView
{
    public EnableEditorView() : base(new EnableViewModel()) { }
}
