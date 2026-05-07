using System;
using System.Globalization;
using System.Windows.Data;

namespace LabVIEWCallPlugin.UI.Converters
{
    /// <summary>
    /// 根据数据类型判断是否为数值类型
    /// </summary>
    public class DataTypeToEditorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string dataType)
            {
                // 转换为小写以便统一比较
                var lowerDataType = dataType.ToLower();

                // 判断是否为枚举类型
                if (lowerDataType.Contains("enum"))
                {
                    return "Enum";
                }
                // 判断是否为整数类型
                else if (lowerDataType.Contains("int") || 
                    lowerDataType.StartsWith("i8") || lowerDataType.StartsWith("i16") || 
                    lowerDataType.StartsWith("i32") || lowerDataType.StartsWith("i64") ||
                    lowerDataType.StartsWith("u8") || lowerDataType.StartsWith("u16") || 
                    lowerDataType.StartsWith("u32") || lowerDataType.StartsWith("u64") ||
                    lowerDataType.Contains("byte") || lowerDataType.Contains("short") || 
                    lowerDataType.Contains("long"))
                {
                    return "Integer";
                }
                // 判断是否为浮点类型
                else if(lowerDataType.Contains("double") || lowerDataType.Contains("float") ||
                         lowerDataType.Contains("single") || lowerDataType.Contains("decimal"))
                {
                    return "Numeric";
                }
                // 判断是否为布尔类型
                else if (lowerDataType.Contains("boolean") || lowerDataType.Contains("bool"))
                {
                    return "Boolean";
                }
                // 其他类型使用文本框
                else
                {
                    return "String";
                }
            }
            return "String";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}