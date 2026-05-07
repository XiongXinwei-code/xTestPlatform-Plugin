using System.Windows;
using System.Windows.Controls;
using LabVIEWCallPlugin.UI.Models;

namespace LabVIEWCallPlugin.UI.Converters
{
    /// <summary>
    /// 根据 DataType 选择合适的编辑模板
    /// </summary>
    public class ValueEditorTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? NumericTemplate { get; set; }
        public DataTemplate? IntegerTemplate { get; set; }
        public DataTemplate? BooleanTemplate { get; set; }
        public DataTemplate? StringTemplate { get; set; }
        public DataTemplate? EnumTemplate { get; set; }

        public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        {
            if (item is LvPanelNode node)
            {
                var dataType = node.Type?.ToLower() ?? string.Empty;
                var value = node.Value ?? string.Empty;

                // 枚举类型 - 检查 Type 是否包含 "enum" 或检查值是否为 JSON 格式
                if (dataType.Contains("enum") || IsEnumJsonFormat(value))
                {
                    return EnumTemplate;
                }
                // 整数类型 - 精确匹配
                else if (dataType.Contains("int") ||
                    dataType.StartsWith("i8") || dataType.StartsWith("i16") ||
                    dataType.StartsWith("i32") || dataType.StartsWith("i64") ||
                    dataType.StartsWith("u8") || dataType.StartsWith("u16") ||
                    dataType.StartsWith("u32") || dataType.StartsWith("u64") ||
                    dataType.Contains("byte") || dataType.Contains("short") ||
                    dataType.Contains("long"))
                {
                    return IntegerTemplate;
                }
                // 浮点数类型 - 转换小写后统一小写匹配
                else if (dataType.Contains("double") || dataType.Contains("float") ||
                         dataType.Contains("single") || dataType.Contains("decimal"))
                {
                    return NumericTemplate;
                }
                // 布尔类型 - 统一匹配 boolean 和 bool
                else if (dataType.Contains("boolean") || dataType.Contains("bool"))
                {
                    return BooleanTemplate;
                }
                // 字符串类型
                else
                {
                    return StringTemplate;
                }
            }

            return StringTemplate;
        }

        /// <summary>
        /// 检查值是否为枚举 JSON 格式: {"String Value":"xxx","Enum Strings":[...]}
        /// </summary>
        private bool IsEnumJsonFormat(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            value = value.Trim();
            return value.StartsWith("{") &&
                   value.Contains("\"String Value\"") &&
                   value.Contains("\"Enum Strings\"");
        }
    }
}