using System;
using System.Globalization;
using System.Windows.Data;
using System.Text.Json;

namespace LabVIEWCallPlugin.UI.Converters
{
    /// <summary>
    /// 多值转换器：解析和格式化枚举值 JSON 字符串
    /// 格式: {"String Value":"add","Enum Strings":["add","subtract","multiply","divide"]}
    /// </summary>
    public class EnumValueConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 1 && values[0] is string jsonString && !string.IsNullOrEmpty(jsonString))
            {
                try
                {
                    using (JsonDocument doc = JsonDocument.Parse(jsonString))
                    {
                        JsonElement root = doc.RootElement;
                        if (root.TryGetProperty("String Value", out JsonElement stringValue))
                        {
                            return stringValue.GetString();
                        }
                    }
                }
                catch (JsonException)
                {
                    // JSON 解析失败，返回原始值
                    return jsonString;
                }
            }
            return values.Length > 0 ? values[0] : null;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            if (value is string selectedValue && targetTypes.Length >= 2 && parameter is string originalJson)
            {
                try
                {
                    using (JsonDocument doc = JsonDocument.Parse(originalJson))
                    {
                        JsonElement root = doc.RootElement;
                        if (root.TryGetProperty("Enum Strings", out JsonElement enumStrings))
                        {
                            // 重构 JSON，更新 String Value
                            string updatedJson = JsonSerializer.Serialize(new
                            {
                                StringValue = selectedValue,
                                EnumStrings = JsonSerializer.Deserialize<string[]>(enumStrings.GetRawText())
                            });
                            return new object[] { updatedJson, Binding.DoNothing };
                        }
                    }
                }
                catch (JsonException)
                {
                    // 如果解析失败，返回选定的值
                }
            }
            return new object[] { value };
        }
    }

    /// <summary>
    /// 转换器：用于提取枚举列表
    /// </summary>
    public class EnumListConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string jsonString && !string.IsNullOrEmpty(jsonString))
            {
                try
                {
                    using (JsonDocument doc = JsonDocument.Parse(jsonString))
                    {
                        JsonElement root = doc.RootElement;
                        if (root.TryGetProperty("Enum Strings", out JsonElement enumStrings))
                        {
                            return JsonSerializer.Deserialize<string[]>(enumStrings.GetRawText());
                        }
                    }
                }
                catch (JsonException)
                {
                    // JSON 解析失败，返回空数组
                }
            }
            return Array.Empty<string>();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}