using System;
using System.Globalization;
using System.Windows.Data;
using System.Text.Json;

namespace LabVIEWCallPlugin.UI.Converters
{
    /// <summary>
    /// 将 LabVIEW 枚举控件的 JSON 描述转换为当前选中的字符串值。
    /// 格式示例: {"String Value":"add","Enum Strings":["add","subtract","multiply","divide"]}
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
                    // JSON 解析失败时，直接返回原始字符串
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
                            // 保留原有枚举项列表，仅更新 String Value
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
                    // JSON 解析失败时，退回下面的原值返回逻辑
                }
            }
            return new object[] { value };
        }
    }

    /// <summary>
    /// 从 LabVIEW 枚举控件的 JSON 描述中提取全部可选项，供 ItemsSource 绑定使用。
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
                    // JSON 解析失败时，返回空列表
                }
            }
            return Array.Empty<string>();
        }

        // 该转换器仅用于单向提供选项列表，反向写回无意义，保持绑定原值不变
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}
