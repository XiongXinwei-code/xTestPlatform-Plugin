using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace CAN.UI.Dbc;

// Grouped WPF views must see the complete projection, not one refresh per row.
internal sealed class DbcProjectionCollection<T> : ObservableCollection<T>
{
    public void ReplaceWith(IReadOnlyList<T> rows)
    {
        if (Count == 0 && rows.Count == 0) return;
        CheckReentrancy();
        Items.Clear();
        foreach (var row in rows) Items.Add(row);
        OnPropertyChanged(new(nameof(Count)));
        OnPropertyChanged(new("Item[]"));
        OnCollectionChanged(new(NotifyCollectionChangedAction.Reset));
    }
}
