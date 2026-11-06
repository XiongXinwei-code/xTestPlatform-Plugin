using System.IO;
using LabVIEWCallPlugin.LVadapter;
using LabVIEWCallPlugin.LVadapter.Interop;

namespace LabVIEWCallPlugin.Execution;

public sealed partial class LabVIEWCallExecutor
{
    // GetParameters reads a value by name for both controls and indicators. Its
    // type descriptor alone cannot tell which direction the connector exposes.
    internal static List<ViParameterNode> CheckLiveConnector(ConnectPanelInfo panel,
        List<ViParameterNode> inputs, List<ViParameterNode> outputs)
    {
        if (panel.VIState != VIState.Idle)
            throw new InvalidOperationException($"VI 当前状态为 {panel.VIState}，仅允许对空闲 VI 设置输入并运行。");
        if (string.IsNullOrWhiteSpace(panel.ControlOut) || string.IsNullOrWhiteSpace(panel.IndicatorOut))
            throw new InvalidDataException("桥未提供完整输入和输出端子清单，无法确认调用配置。");

        var controls = ReadStructure(panel.ControlOut).ToDictionary(n => n.Name, StringComparer.Ordinal);
        var indicators = ReadStructure(panel.IndicatorOut).ToDictionary(n => n.Name, StringComparer.Ordinal);
        foreach (var node in inputs)
        {
            if (!controls.Remove(node.Name, out var live) || live.Missing)
                throw node.Error("实际 VI 中找不到此输入控件，请核对端子名称和方向。");
            if (indicators.ContainsKey(node.Name))
                throw node.Error("实际 VI 有同名输入和输出，桥按名称访问时无法确定目标。");
            CheckLayout(node, live);
        }
        if (controls.Count != 0)
            throw new InvalidDataException($"缺少 VI 输入配置：{string.Join("、", controls.Keys)}。请刷新端子并显式填写输入值，避免沿用上次调用的值。");

        var result = new List<ViParameterNode>(outputs);
        foreach (var node in outputs)
        {
            if (!indicators.Remove(node.Name, out var live) || live.Missing)
                throw node.Error("实际 VI 中找不到此输出指示器，请核对端子名称和方向。");
            CheckLayout(node, live);
        }

        // An unmapped standard error indicator still determines step status. Live
        // metadata must never introduce platform variable targets or expressions.
        foreach (var candidate in indicators.Values)
            if (IsStandardErrorCluster(candidate))
                result.Add(UnmappedOutput(candidate));
        return result;
    }

    private static void CheckLayout(ViParameterNode configured, ViParameterNode live)
    {
        if (live.Missing || configured.Name != live.Name || configured.Wire != ViWireCodec.ParseType(live) ||
            configured.Children.Count != live.Children.Count)
            throw configured.Error("实际端子类型、字段名称或簇顺序已更改，请重新读取连接器。");
        for (int i = 0; i < configured.Children.Count; i++)
            CheckLayout(configured.Children[i], live.Children[i]);
    }

    private static ViParameterNode UnmappedOutput(ViParameterNode original)
    {
        var copy = new ViParameterNode { Name = original.Name, Path = original.Path, TypeName = original.TypeName,
            Literal = "", Source = "Constant", Target = "", Missing = original.Missing, Log = false };
        if (copy.Missing) throw copy.Error("桥返回的错误端子已缺失。");
        copy.Wire = ViWireCodec.ParseType(copy);
        foreach (var field in original.Children) copy.Children.Add(UnmappedOutput(field));
        return copy;
    }

    private static bool IsStandardErrorCluster(ViParameterNode node)
    {
        if (!node.TypeName.Trim().Equals("Cluster", StringComparison.OrdinalIgnoreCase) || node.Children.Count != 3)
            return false;
        return IsField(node.Children[0], "status", LVTypeDescCode.Boolean) &&
               IsField(node.Children[1], "code", LVTypeDescCode.Int32) &&
               IsField(node.Children[2], "source", LVTypeDescCode.String);
    }

    private static bool IsField(ViParameterNode node, string name, LVTypeDescCode type)
        => node.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && ViWireCodec.IsScalar(node.TypeName, type);
}
