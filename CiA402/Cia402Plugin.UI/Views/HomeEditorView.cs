using CiA402.UI.ViewModels;

namespace CiA402.UI.Views;

public sealed class HomeEditorView : AxisEditorView
{
    public HomeEditorView() : base(new HomeViewModel()) { }
}
