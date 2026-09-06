using LabVIEWCallPlugin.Models;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Diagnostics;
using System;

namespace LabVIEWCallPlugin.Converters
{
    /// <summary>
    /// LabVIEW ������� JSON ����ת����
    /// ���ݸ�ʽ: [[·������], {"Index": 0, "Name": "x+y", "Tag": "0-x+y-Double Float", ...}]
    /// </summary>
    public static class LvPanelConverter
    {
        /// <summary>
        /// �� JSON �ַ���ת��Ϊ���νڵ㼯��(���ڵ��б�)
        /// �����ʽ: [
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
                // ����Ϊ JsonDocument
                using var document = JsonDocument.Parse(jsonData);
                var root = document.RootElement;

                if (root.ValueKind != JsonValueKind.Array)
                {
                    Debug.WriteLine("LvPanelConverter: JSON ��Ԫ�ز�������");
                    return new List<LvPanelNode>();
                }

                // �����ڵ��ֵ䣬ʹ��·���ַ�����Ϊ key
                var nodeDict = new Dictionary<string, LvPanelNode>();

                // �����������л���
                foreach (var item in root.EnumerateArray())
                {
                    try
                    {
                        var node = LvPanelNode.FromSerializedItem(item);

                        // ʹ�� Path ��ΪΨһ��ʶ
                        if (node.Path != null && node.Path.Count > 0)
                        {
                            var pathKey = GetPathKey(node.Path);
                            nodeDict[pathKey] = node;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"LvPanelConverter: �����ڵ�ʧ��: {ex.Message}");
                    }
                }

              //  Debug.WriteLine($"LvPanelConverter: �ɹ����� {nodeDict.Count} ���ڵ�");

                // �������νṹ
                var rootNodes = BuildTreeStructure(nodeDict);

             //   Debug.WriteLine($"LvPanelConverter: ��������ɣ����ڵ���: {rootNodes.Count}");
                return rootNodes;
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"LvPanelConverter: JSON �����л�ʧ��: {ex.Message}");
                Debug.WriteLine($"LvPanelConverter: ����λ��: Line {ex.LineNumber}, Position {ex.BytePositionInLine}");
                return new List<LvPanelNode>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LvPanelConverter: ת��ʧ��: {ex.Message}");
                Debug.WriteLine($"LvPanelConverter: ��ջ: {ex.StackTrace}");
                return new List<LvPanelNode>();
            }
        }

        /// <summary>
        /// ���ڵ���ת��Ϊ JSON �ַ���
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

                // �ռ����нڵ㣨��������ڵ㣩
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
                Debug.WriteLine($"LvPanelConverter: ���л�ʧ��: {ex.Message}");
                return "[]";
            }
        }

        /// <summary>
        /// �������νṹ��ʹ��·����ΪΨһ��ʶ��
        /// </summary>
        private static List<LvPanelNode> BuildTreeStructure(Dictionary<string, LvPanelNode> nodeDict)
        {
            var rootNodes = new List<LvPanelNode>();

            foreach (var node in nodeDict.Values)
            {
                // ����ڵ��� ChildNodePath���򹹽����ӹ�ϵ
                if (node.ChildNodePath != null && node.ChildNodePath.Count > 0)
                {
                    foreach (var childPath in node.ChildNodePath)
                    {
                        if (childPath.Count == 0)
                            continue;

                        var childPathKey = GetPathKey(childPath);

                        if (nodeDict.TryGetValue(childPathKey, out var childNode))
                        {
                            // �����ظ����
                            if (!node.Children.Contains(childNode))
                            {
                                node.Children.Add(childNode);
                                childNode.Parent = node;
                             //   Debug.WriteLine($"LvPanelConverter: ����ӽڵ� [{childPathKey}] �����ڵ� [{GetPathKey(node.Path)}]");
                            }
                        }
                        else
                        {
                            Debug.WriteLine($"LvPanelConverter: ���� - δ�ҵ��ӽڵ� [{childPathKey}]");
                        }
                    }
                }

                // �ҳ����ڵ㣨û�и��ڵ�Ľڵ㣩
                if (node.Parent == null)
                {
                    rootNodes.Add(node);
                 //   Debug.WriteLine($"LvPanelConverter: ��Ӹ��ڵ� [{GetPathKey(node.Path)}] - {node.Name}");
                }
            }

            return rootNodes;
        }

        /// <summary>
        /// ��·������ת��Ϊ�ַ��� key
        /// ��ʽ: "3-error in-Cluster|0-status-Boolean"
        /// </summary>
        private static string GetPathKey(List<string> path)
        {
            if (path == null || path.Count == 0)
                return string.Empty;

            return string.Join("|", path);
        }
    }
}