using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
namespace PluginEditor;

internal sealed class SettingsChangeTracker(Action save) : IDisposable
{
    private readonly List<(IEnumerable Items, INotifyCollectionChanged Events)> _collections = new();
    private readonly HashSet<INotifyPropertyChanged> _items = new(ReferenceEqualityComparer.Instance);
    public void Track(IEnumerable collection)
    {
        if (collection is not INotifyCollectionChanged events) throw new ArgumentException("设置集合需要变更通知");
        _collections.Add((collection, events)); events.CollectionChanged += CollectionChanged; RebindItems();
    }
    private void CollectionChanged(object? sender, NotifyCollectionChangedEventArgs args) { RebindItems(); save(); }
    private void ItemChanged(object? sender, PropertyChangedEventArgs args) => save();
    private void RebindItems()
    {
        foreach (var item in _items) item.PropertyChanged -= ItemChanged;
        _items.Clear();
        foreach (var collection in _collections) foreach (var item in collection.Items.OfType<INotifyPropertyChanged>())
            if (_items.Add(item)) item.PropertyChanged += ItemChanged;
    }
    public void Dispose()
    {
        foreach (var collection in _collections) collection.Events.CollectionChanged -= CollectionChanged;
        foreach (var item in _items) item.PropertyChanged -= ItemChanged;
        _collections.Clear(); _items.Clear();
    }
}
