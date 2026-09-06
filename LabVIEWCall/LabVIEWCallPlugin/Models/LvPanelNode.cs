using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LabVIEWCallPlugin.Models
{
    /// <summary>
    /// ֵ��Դ����ö�٣����� UI �󶨣�
    /// </summary>
    public enum ValueSourceType
    {
        /// <summary>
        /// ����ֵ
        /// </summary>
        Constant,

        /// <summary>
        /// ����
        /// </summary>
        Variable,

        /// <summary>
        /// �Ӳ����ļ���ȡ
        /// </summary>
        FromParameterFile
    }

    /// <summary>
    /// LabVIEW �������ڵ�
    /// ���ݸ�ʽ: [[·������], {"Node": {...}, "�ڵ���": ʵ��ֵ}]
    /// </summary>
    public partial class LvPanelNode : ObservableObject
    {
        #region ��������

        /// <summary>
        /// ���ڵ�����
        /// </summary>
        [ObservableProperty]
        private LvPanelNode? _parent;

        /// <summary>
        /// �ӽڵ㼯��
        /// </summary>
        public ObservableCollection<LvPanelNode> Children { get; }

        /// <summary>
        /// �ڵ�·�����飨��: ["3-error in-Cluster","0-status-Boolean"]��
        /// �� JSON ���ݵĵ�һ������Ԫ���ж�ȡ
        /// </summary>
        public List<string> Path { get; set; }

        /// <summary>
        /// �ڵ�����
        /// </summary>
        [ObservableProperty]
        private int _index;

        /// <summary>
        /// �ڵ����ƣ���: x+y, error out, status��
        /// </summary>
        [ObservableProperty]
        private string _name;

        /// <summary>
        /// ��ǩ��Tag����ʽ��Index-Name-Type����: 2-x+y-Double Float��
        /// </summary>
        [ObservableProperty]
        private string _tag;

        /// <summary>
        /// �ڵ�ֵ�����л����ַ���ֵ��
        /// </summary>
        [ObservableProperty]
        private string _value;

        /// <summary>
        /// �ڵ������
        /// </summary>
        [ObservableProperty]
        private string _variable;

        /// <summary>
        /// �������ͣ���: Double Float, Cluster, String, Boolean, I32, Enum U16��
        /// </summary>
        [ObservableProperty]
        private string _type;

        /// <summary>
        /// �Ƿ�ȱʧ
        /// </summary>
        [ObservableProperty]
        private bool _isMissing;

        /// <summary>
        /// �Ƿ��¼��־
        /// </summary>
        [ObservableProperty]
        private bool _log;

        /// <summary>
        /// ͼ������
        /// </summary>
        [ObservableProperty]
        private int _iconIndex;

        /// <summary>
        /// ֵ��Դ���ͣ��ַ�����ʽ���������л���
        /// ��ѡֵ: "Constant", "Variable", "FromParameterFile"
        /// </summary>
        [ObservableProperty]
        private string _valueSourceType = "Constant";

        /// <summary>
        /// �ӽڵ�·�����ϣ��������л���
        /// ��ʽ: [["4-error out-Cluster","0-status-Boolean"], ...]
        /// </summary>
        public List<List<string>> ChildNodePath { get; set; }

        /// <summary>
        /// ������ʾ��ʽ��ʮ���ơ�ʮ�����ơ������ƣ�
        /// </summary>
        [ObservableProperty]
        private string _integerFormat = "Decimal";

        /// <summary>
        /// �Ƿ�չ����UI ״̬��
        /// </summary>
        [ObservableProperty]
        private bool _isExpanded;

        #endregion

        #region ��������

        /// <summary>
        /// �Ƿ�Ϊ���ڵ�
        /// </summary>
        [JsonIgnore]
        public bool IsRootNode => Parent == null;

        /// <summary>
        /// �Ƿ����ӽڵ�
        /// </summary>
        [JsonIgnore]
        public bool HasChildren => Children.Count > 0;

        /// <summary>
        /// �ڵ�㼶������ Path ���ȣ�
        /// </summary>
        [JsonIgnore]
        public int Level => Path?.Count ?? 0;

        /// <summary>
        /// ��ǰ�ڵ�� Tag��·�������һ��Ԫ�أ�
        /// </summary>
        [JsonIgnore]
        public string CurrentTag => Path?.Count > 0 ? Path[Path.Count - 1] : Tag;

        /// <summary>
        /// ���ڵ��·������
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
        /// �Ƿ�Ϊö������
        /// </summary>
        [JsonIgnore]
        public bool IsEnumType => Type?.StartsWith("Enum") == true;

        /// <summary>
        /// ֵ��Դ����ö�٣����� UI �󶨣�
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
        /// ��ȡ������ö�ٵĵ�ǰֵ���� JSON ��ʽ��ȡ "String Value"��
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
        /// ��ȡö���б���� JSON ��ʽ����ȡ "Enum Strings"��
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
        /// ��ȡʵ��ֵ���󣨷����л�������ͻ�ֵ��
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
                    // ����ö������
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
        /// ��ʽ����ʾֵ������ IntegerFormat��
        /// </summary>
        [JsonIgnore]
        public string FormattedValue
        {
            get
            {
                if (ActualValue == null)
                    return string.Empty;

                // ö������ֱ����ʾ��ǰֵ
                if (IsEnumType)
                {
                    return EnumCurrentValue ?? string.Empty;
                }

                // ������������ͣ����ݸ�ʽ��ʾ
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

        #region ���캯��

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

        #region �¼�����

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

        #region ���л�����

        /// <summary>
        /// �����л����ݴ����ڵ�
        /// �����ʽ: [["2-x+y-Double Float"], {"Index": 0, "Name": "x+y", ...}]
        /// ��: [["4-error out-Cluster","0-status-Boolean"], {"Index": 0, "Name": "status", ...}]
        /// </summary>
        public static LvPanelNode FromSerializedItem(JsonElement item)
        {
            if (item.ValueKind != JsonValueKind.Array || item.GetArrayLength() != 2)
                throw new ArgumentException("Invalid serialized item format");

            var pathArrayElement = item[0];
            var dataObject = item[1];

            var node = new LvPanelNode();

            // ����·�����飨��һ������Ԫ�أ�
            if (pathArrayElement.ValueKind == JsonValueKind.Array)
            {
                node.Path = new List<string>();
                foreach (var pathElement in pathArrayElement.EnumerateArray())
                {
                    node.Path.Add(pathElement.GetString() ?? string.Empty);
                }
            }

            // �����ڵ�Ԫ����
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

            // �����ӽڵ�·��
            if (dataObject.TryGetProperty("ChildNodePath", out var childPath) &&
                childPath.ValueKind == JsonValueKind.Array)
            {
                node.ChildNodePath = JsonSerializer.Deserialize<List<List<string>>>(childPath.GetRawText())
                    ?? new List<List<string>>();
            }

            return node;
        }

        /// <summary>
        /// ת��Ϊ���л���ʽ���¸�ʽ�������� "Node" ��װ��
        /// �����ʽ: [["2-x+y-Double Float"], {"Index": 0, "Name": "x+y", ...}]
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

            // ʹ��ʵ�ʵ� Path ����
            var result = new object[] { Path, dataObject };
            return JsonSerializer.SerializeToElement(result);
        }

        /// <summary>
        /// ���л������ڵ��������������ӽڵ㣩
        /// </summary>
        public List<JsonElement> ToSerializedTree()
        {
            var result = new List<JsonElement>();

            // ��ӵ�ǰ�ڵ�
            if (Path != null && Path.Count > 0)
                result.Add(ToSerializedItem());

            // �ݹ���������ӽڵ�
            foreach (var child in Children)
            {
                result.AddRange(child.ToSerializedTree());
            }

            return result;
        }

        #endregion

        #region ö�ٸ�������

        /// <summary>
        /// ����ö��ֵ������ JSON ��ʽ���䣬ֻ���� "String Value"��
        /// </summary>
        /// <param name="newValue">�µ�ö��ֵ</param>
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
                // JSON ����ʧ�ܣ�����
            }
        }

        #endregion

        #region ��������

        /// <summary>
        /// �����ӽڵ�·��
        /// </summary>
        private void UpdateChildNodePath()
        {
            ChildNodePath = Children.Where(c => c.Path != null && c.Path.Count > 0)
                                    .Select(c => c.Path)
                                    .ToList();
        }

        /// <summary>
        /// ��ȡĬ��ֵ
        /// </summary>
        private object GetDefaultValue()
        {
            // ö�����ͷ��ؿ��ַ���
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
        /// ���л�ֵ
        /// </summary>
        private string SerializeValue(object? value)
        {
            if (value == null)
                return string.Empty;

            // ö��������Ҫ���� JSON ��ʽ
            if (IsEnumType && value is string enumValue)
            {
                // ����Ѿ��� JSON ��ʽ��ֱ�ӷ���
                if (enumValue.StartsWith("{"))
                    return enumValue;

                // ���򣬳��Ը���ö��ֵ
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
        /// �����ӽڵ㣨���� Tag��
        /// </summary>
        public LvPanelNode? FindChild(string tag)
        {
            return Children.FirstOrDefault(c => c.Tag == tag || c.CurrentTag == tag);
        }

        /// <summary>
        /// ����·�����ҽڵ�
        /// </summary>
        public LvPanelNode? FindByPath(List<string> pathArray)
        {
            if (pathArray == null || pathArray.Count == 0)
                return this;

            // ���·��ƥ�䵱ǰ�ڵ�
            if (Path != null && Path.SequenceEqual(pathArray))
                return this;

            // �ݹ�����ӽڵ�
            foreach (var child in Children)
            {
                var found = child.FindByPath(pathArray);
                if (found != null)
                    return found;
            }

            return null;
        }

        /// <summary>
        /// ����Ƿ�Ϊָ���ڵ���ӽڵ�
        /// </summary>
        public bool IsChildOf(LvPanelNode potentialParent)
        {
            if (potentialParent == null || Path == null || potentialParent.Path == null)
                return false;

            // �ӽڵ��·��Ӧ���Ը��ڵ��·����ͷ
            if (Path.Count <= potentialParent.Path.Count)
                return false;

            for (int i = 0; i < potentialParent.Path.Count; i++)
            {
                if (Path[i] != potentialParent.Path[i])
                    return false;
            }

            // ȷ����ֱ���ӽڵ㣨·�����Ȳ�1��
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
    /// �Զ��� JSON �����������ԣ�����ö������
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
    /// �ڵ�Ԫ���ݣ����ڶ������л���
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