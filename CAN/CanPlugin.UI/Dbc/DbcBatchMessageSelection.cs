using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;

namespace CAN.UI.Dbc;

/// <summary>Editor-only selection state. It never mutates saved message settings.</summary>
public sealed class DbcBatchMessageSelection : INotifyPropertyChanged
{
    private string _filter = "";
    private int _selectedCount;
    private bool _selectingMany;
    private readonly DbcProjectionCollection<DbcBatchMessageRow> _visible = [];
    public DbcBatchMessageSelection(IEnumerable<DbcMessageChoice> choices, ISet<(uint Id, bool Extended)> configured, bool replaceSelection = false)
    {
        ReplaceSelection = replaceSelection;
        Rows = choices.Select(choice => new DbcBatchMessageRow(choice,
            configured.Contains((choice.Message.Id, choice.Message.Extended)), replaceSelection)).ToArray();
        _selectedCount = Rows.Count(row => row.Selected);
        PropertyChangedEventHandler changed = OnRowChanged;
        foreach (var row in Rows) row.PropertyChanged += changed;
        RefreshFilter();
    }
    public bool ReplaceSelection { get; }
    public IReadOnlyList<DbcBatchMessageRow> Rows { get; }
    public ObservableCollection<DbcBatchMessageRow> VisibleRows => _visible;
    public string Filter
    {
        get => _filter;
        set { if (_filter == value) return; _filter = value ?? ""; RefreshFilter(); PropertyChanged?.Invoke(this, new(nameof(Filter))); }
    }
    public string Summary => $"已选择 {_selectedCount} 条；当前显示 {VisibleRows.Count} / {Rows.Count} 条。" + (ReplaceSelection
        ? "应用后保留勾选报文，移除取消勾选报文；保留项的信号、映射、RC/CRC 和周期不变。当前 DBC 中不存在的旧报文请在主列表移除。"
        : "已添加的报文保持原配置。");
    public event PropertyChangedEventHandler? PropertyChanged;
    public void SelectVisible(bool selected)
    {
        _selectingMany = true;
        try { foreach (var row in VisibleRows) row.Selected = selected; }
        finally { _selectingMany = false; PropertyChanged?.Invoke(this, new(nameof(Summary))); }
    }
    private void RefreshFilter()
    {
        string filter = _filter.Trim();
        _visible.ReplaceWith(filter.Length == 0 ? Rows : Rows.Where(row => row.Matches(filter)).ToArray());
        PropertyChanged?.Invoke(this, new(nameof(Summary)));
    }

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not DbcBatchMessageRow row || e.PropertyName != nameof(row.Selected)) return;
        _selectedCount += row.Selected ? 1 : -1;
        if (!_selectingMany) PropertyChanged?.Invoke(this, new(nameof(Summary)));
    }
}

public sealed class DbcBatchMessageRow(DbcMessageChoice choice, bool configured, bool replaceSelection = false) : INotifyPropertyChanged
{
    private bool _selected = configured && replaceSelection;
    public DbcMessageChoice Choice { get; } = choice;
    public string Display { get; } = choice.Display;
    private readonly string _decimalId = choice.Message.Id.ToString(CultureInfo.InvariantCulture);
    internal bool Matches(string filter) => Display.Contains(filter, StringComparison.OrdinalIgnoreCase)
        || _decimalId.Contains(filter, StringComparison.Ordinal);
    public bool CanSelect => replaceSelection || !configured;
    public string State => configured ? "已添加" : "可添加";
    public bool Selected
    {
        get => _selected;
        set { if (!CanSelect || _selected == value) return; _selected = value; PropertyChanged?.Invoke(this, new(nameof(Selected))); }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
}
