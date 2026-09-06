using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LabVIEWCallPlugin.UI.Models
{
    /// <summary>
    /// 值来源类型枚举（用于 UI 绑定）
    /// </summary>
    public enum ValueSourceType
    {
        /// <summary>
        /// 常量值
        /// </summary>
        Constant,

        /// <summary>
        /// 变量
        /// </summary>
        Variable,

        /// <summary>
        /// 从参数文件读取
        /// </summary>
        FromParameterFile
    }

    /// <summary>
    /// LabVIEW 连接面板节点
    /// 数据格式: [[路径数组], {"Node": {...}, "节点名": 实际值}]
    /// </summary>
    public partial class LvPanelNode : ObservableObject
    {
        #region 核心属性

        /// <summary>
        /// 父节点引用
        /// </summary>
        [ObservableProperty]
        private LvPanelNode? _parent;

        /// <summary>
        /// 子节点集合
        /// </summary>
        public ObservableCollection<LvPanelNode> Children { get; }

        /// <summary>
        /// 节点路径数组（如: ["3-error in-Cluster","0-status-Boolean"]）
        /// 从 JSON 数据的第一个数组元素中读取
        /// </summary>
        public List<string> Path { get; set; }

        /// <summary>
        /// 节点索引
        /// </summary>
        [ObservableProperty]
        private int _index;

        /// <summary>
        /// 节点名称（如: x+y, error out, status）
        /// </summary>
        [ObservableProperty]
        private string _name;

        /// <summary>
        /// 标签（Tag）格式：Index-Name-Type（如: 2-x+y-Double Float）
        /// </summary>
        [ObservableProperty]
        private string _tag;

        /// <summary>
        /// 节点值（序列化的字符串值）
        /// </summary>
        [ObservableProperty]
        private string _value;

        /// <summary>
        /// 节点变量名
        /// </summary>
        [ObservableProperty]
        private string _variable;

        /// <summary>
        /// 数据类型（如: Double Float, Cluster, String, Boolean, I32, Enum U16）
        /// </summary>
        [ObservableProperty]
        private string _type;

        /// <summary>
        /// 是否缺失
        /// </summary>
        [ObservableProperty]
        private bool _isMissing;

        /// <summary>
        /// 是否记录日志
        /// </summary>
        [ObservableProperty]
        private bool _log;

        /// <summary>
        /// 图标索引
        /// </summary>
        [ObservableProperty]
        private int _iconIndex;

        /// <summary>
        /// 值来源类型（字符串形式，用于序列化）
        /// 可选值: "Constant", "Variable", "FromParameterFile"
        /// </summary>
        [ObservableProperty]
        private string _valueSourceType = "Constant";

        /// <summary>
        /// 子节点路径集合（用于序列化）
        /// 格式: [["4-error out-Cluster","0-status-Boolean"], ...]
        /// </summary>
        public List<List<string>> ChildNodePath { get; set; }

        /// <summary>
        /// 整数显示格式（十进制、十六进制、二进制）
        /// </summary>
        [ObservableProperty]
        private string _integerFormat = "Decimal";

        /// <summary>
        /// 是否展开（UI 状态）
        /// </summary>
        [ObservableProperty]
        private bool _isExpanded;

        #endregion

        #region 计算属性

        /// <summary>
        /// 是否为根节点
        /// </summary>
        [JsonIgnore]
        public bool IsRootNode => Parent == null;

        /// <summary>
        /// 是否有子节点
        /// </summary>
        [JsonIgnore]
        public bool HasChildren => Children.Count > 0;

        /// <summary>
        /// 节点层级（根据 Path 长度）
        /// </summary>
        [JsonIgnore]
        public int Level => Path?.Count ?? 0;

        /// <summary>
        /// 当前节点的 Tag（路径的最后一个元素）
        /// </summary>
        [JsonIgnore]
        public string CurrentTag => Path?.Count > 0 ? Path[Path.Count - 1] : Tag;

        /// <summary>
        /// 父节点的路径数组
        /// </summary>
        [JsonIgnore]
        public List<string>? ParentPath
        {
            get
            {
                if (Path == null || Path.Count <= 1)
                    return null;

                return Path.Take(Path.Count - 1).ToList();
            }
        }

        /// <summary>
        /// 是否为枚举类型
        /// </summary>
        [JsonIgnore]
        public bool IsEnumType => Type?.StartsWith("Enum") == true;

        /// <summary>
        /// 值来源类型枚举（用于 UI 绑定）
        /// </summary>
        [JsonIgnore]
        public ValueSourceType ValueSourceTypeEnum
        {
            get => Enum.TryParse<ValueSourceType>(ValueSourceType, out var result)
                ? result
                : Models.ValueSourceType.Constant;
            set
            {
                ValueSourceType = value.ToString();
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 获取或设置枚举的当前值（从 JSON 格式读取 "String Value"）
        /// </summary>
        [JsonIgnore]
        public string? EnumCurrentValue
        {
            get
            {
                if (!IsEnumType || string.IsNullOrEmpty(Value))
                    return null;

                try
                {
                    using (JsonDocument doc = JsonDocument.Parse(Value))
                    {
                        if (doc.RootElement.TryGetProperty("String Value", out JsonElement stringValue))
                        {
                            return stringValue.GetString();
                        }
                    }
                }
                catch (JsonException)
                {
                }

                return null;
            }
            set
            {
                if (!IsEnumType || value == null)
                    return;

                UpdateEnumValue(value);
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 获取枚举列表（从 JSON 格式中提取 "Enum Strings"）
        /// </summary>
        [JsonIgnore]
        public string[]? EnumValues
        {
            get
            {
                if (!IsEnumType || string.IsNullOrEmpty(Value))
                    return null;

                try
                {
                    using (JsonDocument doc = JsonDocument.Parse(Value))
                    {
                        if (doc.RootElement.TryGetProperty("Enum Strings", out JsonElement enumStrings))
                        {
                            return JsonSerializer.Deserialize<string[]>(enumStrings.GetRawText());
                        }
                    }
                }
                catch (JsonException)
                {
                }

                return null;
            }
        }

        /// <summary>
        /// 获取实际值对象（反序列化后的类型化值）
        /// </summary>
        [JsonIgnore]
        public object? ActualValue
        {
            get
            {
                if (string.IsNullOrEmpty(Value))
                    return GetDefaultValue();

                try
                {
                    // 处理枚举类型
                    if (IsEnumType)
                    {
                        return EnumCurrentValue ?? string.Empty;
                    }

                    return Type switch
                    {
                        "Boolean" => bool.Parse(Value),
                        "I32" or "I16" or "I8" => int.Parse(Value),
                        "U32" or "U16" or "U8" => uint.Parse(Value),
                        "I64" => long.Parse(Value),
                        "U64" => ulong.Parse(Value),
                        "Double Float" or "Single Float" => double.Parse(Value),
                        "String" => Value.Trim('"'),
                        "Cluster" => JsonSerializer.Deserialize<Dictionary<string, object>>(Value),
                        _ => Value
                    };
                }
                catch
                {
                    return GetDefaultValue();
                }
            }
            set
            {
                Value = SerializeValue(value);
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 格式化显示值（根据 IntegerFormat）
        /// </summary>
        [JsonIgnore]
        public string FormattedValue
        {
            get
            {
                if (ActualValue == null)
                    return string.Empty;

                // 枚举类型直接显示当前值
                if (IsEnumType)
                {
                    return EnumCurrentValue ?? string.Empty;
                }

                // 如果是整数类型，根据格式显示
                if (Type is "I32" or "I16" or "I8" or "U32" or "U16" or "U8" or "I64" or "U64")
                {
                    if (ActualValue is int intVal)
                    {
                        return IntegerFormat switch
                        {
                            "Hexadecimal" => $"0x{intVal:X}",
                            "Binary" => Convert.ToString(intVal, 2),
                            _ => intVal.ToString()
                        };
                    }
                    else if (ActualValue is uint uintVal)
                    {
                        return IntegerFormat switch
                        {
                            "Hexadecimal" => $"0x{uintVal:X}",
                            "Binary" => Convert.ToString(uintVal, 2),
                            _ => uintVal.ToString()
                        };
                    }
                    else if (ActualValue is long longVal)
                    {
                        return IntegerFormat switch
                        {
                            "Hexadecimal" => $"0x{longVal:X}",
                            "Binary" => Convert.ToString(longVal, 2),
                            _ => longVal.ToString()
                        };
                    }
                    else if (ActualValue is ulong ulongVal)
                    {
                        return IntegerFormat switch
                        {
                            "Hexadecimal" => $"0x{ulongVal:X}",
                            "Binary" => Convert.ToString((long)ulongVal, 2),
                            _ => ulongVal.ToString()
                        };
                    }
                }

                return ActualValue.ToString() ?? string.Empty;
            }
        }

        #endregion

        #region 构造函数

        public LvPanelNode()
        {
            _name = string.Empty;
            _tag = string.Empty;
            _value = string.Empty;
            _variable = string.Empty;
            _type = string.Empty;
            Path = new List<string>();
            Children = new ObservableCollection<LvPanelNode>();
            ChildNodePath = new List<List<string>>();

            Children.CollectionChanged += OnChildrenCollectionChanged;
        }

        #endregion

        #region 事件处理

        private void OnChildrenCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (LvPanelNode child in e.NewItems)
                {
                    if (child.Parent != this)
                        child.Parent = this;
                }
            }

            if (e.OldItems != null)
            {
                foreach (LvPanelNode child in e.OldItems)
                {
                    if (child.Parent == this)
                        child.Parent = null;
                }
            }

            OnPropertyChanged(nameof(HasChildren));
            UpdateChildNodePath();
        }

        #endregion

        #region 序列化方法

        /// <summary>
        /// 从序列化数据创建节点
        /// 输入格式: [["2-x+y-Double Float"], {"Index": 0, "Name": "x+y", ...}]
        /// 或: [["4-error out-Cluster","0-status-Boolean"], {"Index": 0, "Name": "status", ...}]
        /// </summary>
        public static LvPanelNode FromSerializedItem(JsonElement item)
        {
            if (item.ValueKind != JsonValueKind.Array || item.GetArrayLength() != 2)
                throw new ArgumentException("Invalid serialized item format");

            var pathArrayElement = item[0];
            var dataObject = item[1];

            var node = new LvPanelNode();

            // 解析路径数组（第一个数组元素）
            if (pathArrayElement.ValueKind == JsonValueKind.Array)
            {
                node.Path = new List<string>();
                foreach (var pathElement in pathArrayElement.EnumerateArray())
                {
                    node.Path.Add(pathElement.GetString() ?? string.Empty);
                }
            }

            // 解析节点元数据
            if (dataObject.TryGetProperty("Index", out var indexProp))
            {
                node.Index = indexProp.GetInt32();
            }
            if (dataObject.TryGetProperty("Name", out var nameProp))
            {
                node.Name = nameProp.GetString() ?? string.Empty;
            }
            if (dataObject.TryGetProperty("Tag", out var tagProp))
            {
                node.Tag = tagProp.GetString() ?? string.Empty;
            }
            if (dataObject.TryGetProperty("Value", out var valueProp))
            {
                node.Value = valueProp.GetString() ?? string.Empty;
            }
            if (dataObject.TryGetProperty("Type", out var typeProp))
            {
                node.Type = typeProp.GetString() ?? string.Empty;
            }
            if (dataObject.TryGetProperty("isMissing", out var isMissingProp))
            {
                node.IsMissing = isMissingProp.GetBoolean();
            }
            if (dataObject.TryGetProperty("Log", out var logProp))
            {
                node.Log = logProp.GetBoolean();
            }
            if (dataObject.TryGetProperty("IconIndex", out var iconIndexProp))
            {
                node.IconIndex = iconIndexProp.GetInt32();
            }
            if (dataObject.TryGetProperty("ValueSourceType", out var valueSourceTypeProp))
            {
                node.ValueSourceType = valueSourceTypeProp.GetString() ?? "Constant";
            }
            if (dataObject.TryGetProperty("Variable", out var variableProp))
            {
                node.Variable = variableProp.GetString() ?? "Constant";
            }

            // 解析子节点路径
            if (dataObject.TryGetProperty("ChildNodePath", out var childPath) &&
                childPath.ValueKind == JsonValueKind.Array)
            {
                node.ChildNodePath = JsonSerializer.Deserialize<List<List<string>>>(childPath.GetRawText())
                    ?? new List<List<string>>();
            }

            return node;
        }

        /// <summary>
        /// 转换为序列化格式（新格式，不包含 "Node" 包装）
        /// 输出格式: [["2-x+y-Double Float"], {"Index": 0, "Name": "x+y", ...}]
        /// </summary>
        public JsonElement ToSerializedItem()
        {
            var dataObject = new
            {
                Index = Index,
                Name = Name,
                Tag = Tag,
                Value = Value,
                Variable = Variable,
                Type = Type,
                isMissing = IsMissing,
                Log = Log,
                IconIndex = IconIndex,
                ValueSourceType = ValueSourceType,
                ChildNodePath = ChildNodePath
            };

            // 使用实际的 Path 数组
            var result = new object[] { Path, dataObject };
            return JsonSerializer.SerializeToElement(result);
        }

        /// <summary>
        /// 序列化整个节点树（包括所有子节点）
        /// </summary>
        public List<JsonElement> ToSerializedTree()
        {
            var result = new List<JsonElement>();

            // 添加当前节点
            if (Path != null && Path.Count > 0)
                result.Add(ToSerializedItem());

            // 递归添加所有子节点
            foreach (var child in Children)
            {
                result.AddRange(child.ToSerializedTree());
            }

            return result;
        }

        #endregion

        #region 枚举辅助方法

        /// <summary>
        /// 更新枚举值（保持 JSON 格式不变，只更新 "String Value"）
        /// </summary>
        /// <param name="newValue">新的枚举值</param>
        public void UpdateEnumValue(string newValue)
        {
            if (!IsEnumType || string.IsNullOrEmpty(Value))
                return;

            try
            {
                using (JsonDocument doc = JsonDocument.Parse(Value))
                {
                    if (doc.RootElement.TryGetProperty("Enum Strings", out JsonElement enumStrings))
                    {
                        var updatedJson = JsonSerializer.Serialize(new
                        {
                            StringValue = newValue,
                            EnumStrings = JsonSerializer.Deserialize<string[]>(enumStrings.GetRawText())
                        }, new JsonSerializerOptions { PropertyNamingPolicy = new EnumPropertyNamingPolicy() });

                        Value = updatedJson;
                        OnPropertyChanged(nameof(Value));
                        OnPropertyChanged(nameof(EnumCurrentValue));
                        OnPropertyChanged(nameof(ActualValue));
                        OnPropertyChanged(nameof(FormattedValue));
                    }
                }
            }
            catch (JsonException)
            {
                // JSON 解析失败，忽略
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 更新子节点路径
        /// </summary>
        private void UpdateChildNodePath()
        {
            ChildNodePath = Children.Where(c => c.Path != null && c.Path.Count > 0)
                                    .Select(c => c.Path)
                                    .ToList();
        }

        /// <summary>
        /// 获取默认值
        /// </summary>
        private object GetDefaultValue()
        {
            // 枚举类型返回空字符串
            if (IsEnumType)
                return string.Empty;

            return Type switch
            {
                "Boolean" => false,
                "I32" or "I16" or "I8" or "U32" or "U16" or "U8" => 0,
                "I64" or "U64" => 0L,
                "Double Float" or "Single Float" => 0.0,
                "String" => string.Empty,
                "Cluster" => new Dictionary<string, object>(),
                _ => string.Empty
            };
        }

        /// <summary>
        /// 序列化值
        /// </summary>
        private string SerializeValue(object? value)
        {
            if (value == null)
                return string.Empty;

            // 枚举类型需要保持 JSON 格式
            if (IsEnumType && value is string enumValue)
            {
                // 如果已经是 JSON 格式，直接返回
                if (enumValue.StartsWith("{"))
                    return enumValue;

                // 否则，尝试更新枚举值
                UpdateEnumValue(enumValue);
                return Value;
            }

            return Type switch
            {
                "String" => $"\"{value}\"",
                "Cluster" => JsonSerializer.Serialize(value),
                _ => value.ToString() ?? string.Empty
            };
        }

        /// <summary>
        /// 查找子节点（根据 Tag）
        /// </summary>
        public LvPanelNode? FindChild(string tag)
        {
            return Children.FirstOrDefault(c => c.Tag == tag || c.CurrentTag == tag);
        }

        /// <summary>
        /// 根据路径查找节点
        /// </summary>
        public LvPanelNode? FindByPath(List<string> pathArray)
        {
            if (pathArray == null || pathArray.Count == 0)
                return this;

            // 如果路径匹配当前节点
            if (Path != null && Path.SequenceEqual(pathArray))
                return this;

            // 递归查找子节点
            foreach (var child in Children)
            {
                var found = child.FindByPath(pathArray);
                if (found != null)
                    return found;
            }

            return null;
        }

        /// <summary>
        /// 检查是否为指定节点的子节点
        /// </summary>
        public bool IsChildOf(LvPanelNode potentialParent)
        {
            if (potentialParent == null || Path == null || potentialParent.Path == null)
                return false;

            // 子节点的路径应该以父节点的路径开头
            if (Path.Count <= potentialParent.Path.Count)
                return false;

            for (int i = 0; i < potentialParent.Path.Count; i++)
            {
                if (Path[i] != potentialParent.Path[i])
                    return false;
            }

            // 确保是直接子节点（路径长度差1）
            return Path.Count == potentialParent.Path.Count + 1;
        }

        #endregion

        public override string ToString()
        {
            var pathStr = Path != null ? string.Join(" > ", Path) : "No Path";
            return $"[{pathStr}] {Name} ({Type}) = {ActualValue}";
        }
    }

    /// <summary>
    /// 自定义 JSON 属性命名策略，用于枚举类型
    /// </summary>
    internal class EnumPropertyNamingPolicy : JsonNamingPolicy
    {
        public override string ConvertName(string name)
        {
            return name switch
            {
                "StringValue" => "String Value",
                "EnumStrings" => "Enum Strings",
                _ => name
            };
        }
    }

    /// <summary>
    /// 节点元数据（用于独立序列化）
    /// </summary>
    public class NodeMetadata
    {
        public int Index { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Tag { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Variable { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool isMissing { get; set; }
        public bool Log { get; set; }
        public int IconIndex { get; set; }
        public string ValueSourceType { get; set; } = "Constant";
        public List<List<string>> ChildNodePath { get; set; } = new List<List<string>>();
    }
}