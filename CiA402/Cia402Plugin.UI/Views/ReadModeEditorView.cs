using CiA402.UI.ViewModels;

namespace CiA402.UI.Views;

public sealed class ReadModeEditorView : AxisEditorView
{
    public ReadModeEditorView() : base(new ReadModeViewModel()) { }
}
