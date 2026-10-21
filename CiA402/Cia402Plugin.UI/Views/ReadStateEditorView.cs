using CiA402.UI.ViewModels;

namespace CiA402.UI.Views;

public sealed class ReadStateEditorView : AxisEditorView
{
    public ReadStateEditorView() : base(new ReadStateViewModel()) { }
}
