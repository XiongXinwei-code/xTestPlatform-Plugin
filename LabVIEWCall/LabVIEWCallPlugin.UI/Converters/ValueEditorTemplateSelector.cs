using LabVIEWCallPlugin.Models;
using System.Windows;
using System.Windows.Controls;
using LabVIEWCallPlugin.UI.Models;

namespace LabVIEWCallPlugin.UI.Converters
{
    /// <summary>
    /// ���� DataType ѡ����ʵı༭ģ��
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

                // ö������ - ��� Type �Ƿ���� "enum" ����ֵ�Ƿ�Ϊ JSON ��ʽ
                if (dataType.Contains("enum") || IsEnumJsonFormat(value))
                {
                    return EnumTemplate;
                }
                // �������� - ��ȷƥ��
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
                // ���������� - ת��Сд��ͳһСдƥ��
                else if (dataType.Contains("double") || dataType.Contains("float") ||
                         dataType.Contains("single") || dataType.Contains("decimal"))
                {
                    return NumericTemplate;
                }
                // �������� - ͳһƥ�� boolean �� bool
                else if (dataType.Contains("boolean") || dataType.Contains("bool"))
                {
                    return BooleanTemplate;
                }
                // �ַ�������
                else
                {
                    return StringTemplate;
                }
            }

            return StringTemplate;
        }

        /// <summary>
        /// ���ֵ�Ƿ�Ϊö�� JSON ��ʽ: {"String Value":"xxx","Enum Strings":[...]}
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