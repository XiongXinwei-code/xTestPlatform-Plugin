using LabVIEWCallPlugin.Execution;
using LabVIEWCallPlugin.UI.Models;
using System.Text.Json;

namespace LabVIEWCallPlugin.UI.Converters;

public static class LvPanelConverter
{
    public static List<LvPanelNode> ConvertFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        LabVIEWCallExecutor.ValidateParameterStructure(json);
        using var document = JsonDocument.Parse(json);
        var nodes = document.RootElement.EnumerateArray()
            .Select(LvPanelNode.FromSerializedItem).ToList();
        var paths = nodes.ToDictionary(node => JsonSerializer.Serialize(node.Path));
        foreach (var parent in nodes)
        {
            // Adding Children rebuilds ChildNodePath. Snapshot its imported order first.
            foreach (var path in parent.ChildNodePath.ToArray())
                parent.Children.Add(paths[JsonSerializer.Serialize(path)]);
        }
        return nodes.Where(node => node.Parent == null).ToList();
    }

    public static string ConvertToJson(IEnumerable<LvPanelNode> nodes) =>
        JsonSerializer.Serialize(nodes.SelectMany(node => node.ToSerializedTree()));
}
