using CiA402.UI.ViewModels;

namespace CiA402.UI.Views;

public sealed class ReadDiagnosticsEditorView : AxisEditorView
{
    public ReadDiagnosticsEditorView() : base(new ReadDiagnosticsViewModel()) { }
}
