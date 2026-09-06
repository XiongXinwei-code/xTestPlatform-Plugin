using LabVIEWCallPlugin.UI.Models;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Diagnostics;
using System;

namespace LabVIEWCallPlugin.UI.Converters
{
    /// <summary>
    /// LabVIEW 连接面板 JSON 数据转换器
    /// 数据格式: [[路径数组], {"Index": 0, "Name": "x+y", "Tag": "0-x+y-Double Float", ...}]
    /// </summary>
    public static class LvPanelConverter
    {
        /// <summary>
        /// 将 JSON 字符串转换为树形节点集合(根节点列表)
        /// 输入格式: [
        ///   [["0-y-Double Float"], {"Index": 0, "Name": "y", "Tag": "0-y-Double Float", "Value": "0", "Type": "Double Float", ...}],
        ///   [["3-error in-Cluster"], {"Index": 3, "Name": "error in", "Tag": "3-error in-Cluster", "Type": "Cluster", ...}],
        ///   [["3-error in-Cluster","0-status-Boolean"], {"Index": 0, "Name": "status", "Tag": "0-status-Boolean", ...}]
        /// ]
        /// </summary>
        public static List<LvPanelNode> ConvertFromJson(string jsonData)
        {
            if (string.IsNullOrWhiteSpace(jsonData))
            {
                return new List<LvPanelNode>();
            }

            try
            {
                // 解析为 JsonDocument
                using var document = JsonDocument.Parse(jsonData);
                var root = document.RootElement;

                if (root.ValueKind != JsonValueKind.Array)
                {
                    Debug.WriteLine("LvPanelConverter: JSON 根元素不是数组");
                    return new List<LvPanelNode>();
                }

                // 创建节点字典，使用路径字符串作为 key
                var nodeDict = new Dictionary<string, LvPanelNode>();

                // 遍历所有序列化项
                foreach (var item in root.EnumerateArray())
                {
                    try
                    {
                        var node = LvPanelNode.FromSerializedItem(item);

                        // 使用 Path 作为唯一标识
                        if (node.Path != null && node.Path.Count > 0)
                        {
                            var pathKey = GetPathKey(node.Path);
                            nodeDict[pathKey] = node;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"LvPanelConverter: 解析节点失败: {ex.Message}");
                    }
                }

              //  Debug.WriteLine($"LvPanelConverter: 成功解析 {nodeDict.Count} 个节点");

                // 构建树形结构
                var rootNodes = BuildTreeStructure(nodeDict);

             //   Debug.WriteLine($"LvPanelConverter: 构建树完成，根节点数: {rootNodes.Count}");
                return rootNodes;
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"LvPanelConverter: JSON 反序列化失败: {ex.Message}");
                Debug.WriteLine($"LvPanelConverter: 错误位置: Line {ex.LineNumber}, Position {ex.BytePositionInLine}");
                return new List<LvPanelNode>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LvPanelConverter: 转换失败: {ex.Message}");
                Debug.WriteLine($"LvPanelConverter: 堆栈: {ex.StackTrace}");
                return new List<LvPanelNode>();
            }
        }

        /// <summary>
        /// 将节点树转换为 JSON 字符串
        /// </summary>
        public static string ConvertToJson(IEnumerable<LvPanelNode> rootNodes)
        {
            if (rootNodes == null)
            {
                return "[]";
            }

            try
            {
                var allItems = new List<JsonElement>();

                // 收集所有节点（包括子孙节点）
                foreach (var root in rootNodes)
                {
                    allItems.AddRange(root.ToSerializedTree());
                }

                return JsonSerializer.Serialize(allItems, new JsonSerializerOptions
                {
                    WriteIndented = false,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LvPanelConverter: 序列化失败: {ex.Message}");
                return "[]";
            }
        }

        /// <summary>
        /// 构建树形结构（使用路径作为唯一标识）
        /// </summary>
        private static List<LvPanelNode> BuildTreeStructure(Dictionary<string, LvPanelNode> nodeDict)
        {
            var rootNodes = new List<LvPanelNode>();

            foreach (var node in nodeDict.Values)
            {
                // 如果节点有 ChildNodePath，则构建父子关系
                if (node.ChildNodePath != null && node.ChildNodePath.Count > 0)
                {
                    foreach (var childPath in node.ChildNodePath)
                    {
                        if (childPath.Count == 0)
                            continue;

                        var childPathKey = GetPathKey(childPath);

                        if (nodeDict.TryGetValue(childPathKey, out var childNode))
                        {
                            // 避免重复添加
                            if (!node.Children.Contains(childNode))
                            {
                                node.Children.Add(childNode);
                                childNode.Parent = node;
                             //   Debug.WriteLine($"LvPanelConverter: 添加子节点 [{childPathKey}] 到父节点 [{GetPathKey(node.Path)}]");
                            }
                        }
                        else
                        {
                            Debug.WriteLine($"LvPanelConverter: 警告 - 未找到子节点 [{childPathKey}]");
                        }
                    }
                }

                // 找出根节点（没有父节点的节点）
                if (node.Parent == null)
                {
                    rootNodes.Add(node);
                 //   Debug.WriteLine($"LvPanelConverter: 添加根节点 [{GetPathKey(node.Path)}] - {node.Name}");
                }
            }

            return rootNodes;
        }

        /// <summary>
        /// 将路径数组转换为字符串 key
        /// 格式: "3-error in-Cluster|0-status-Boolean"
        /// </summary>
        private static string GetPathKey(List<string> path)
        {
            if (path == null || path.Count == 0)
                return string.Empty;

            return string.Join("|", path);
        }
    }
}