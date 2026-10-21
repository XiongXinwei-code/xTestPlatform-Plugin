using CiA402.UI.ViewModels;

namespace CiA402.UI.Views;

public sealed class WriteCsvTargetEditorView : AxisEditorView
{
    public WriteCsvTargetEditorView() : base(new WriteCsvTargetViewModel()) { }
}
