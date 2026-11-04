using System.Collections.Specialized;
using CAN.Dbc;
using CAN.UI.Dbc;
using Xunit;

namespace CAN.Tests;

public sealed class DbcBatchFilterProjectionTests
{
    [Fact]
    public void Filtering_a_thousand_messages_publishes_one_view_and_preserves_hidden_selections()
    {
        string text = string.Join('\n', Enumerable.Range(0, 1000).Select(i => $"BO_ {i} Msg{i:D4}: 1 ECU"));
        var choices = DbcDatabase.ParseCatalog(text).Messages.Select(m => new DbcMessageChoice(m));
        var picker = new DbcBatchMessageSelection(choices, new HashSet<(uint, bool)> { (999, false) }, true);
        var changes = new List<NotifyCollectionChangedAction>();
        picker.VisibleRows.CollectionChanged += (_, e) => changes.Add(e.Action);

        picker.Filter = "Msg00";
        Assert.Equal(100, picker.VisibleRows.Count);
        Assert.Equal(new[] { NotifyCollectionChangedAction.Reset }, changes);
        picker.VisibleRows[0].Selected = true;
        picker.Filter = "";
        Assert.Equal(1000, picker.VisibleRows.Count);
        Assert.Equal(new uint[] { 0, 999 }, picker.Rows.Where(r => r.Selected).Select(r => r.Choice.Message.Id));
        Assert.Equal(2, changes.Count);
        Assert.All(changes, value => Assert.Equal(NotifyCollectionChangedAction.Reset, value));
    }
}
