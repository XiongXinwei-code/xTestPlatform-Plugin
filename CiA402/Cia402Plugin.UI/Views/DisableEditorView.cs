using CiA402.UI.ViewModels;

namespace CiA402.UI.Views;

public sealed class DisableEditorView : AxisEditorView
{
    public DisableEditorView() : base(new DisableViewModel()) { }
}
