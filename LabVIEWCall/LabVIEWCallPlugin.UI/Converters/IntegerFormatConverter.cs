using System;
using System.Globalization;
using System.Windows.Data;

namespace LabVIEWCallPlugin.UI.Converters
{
    /// <summary>
    /// 整数格式转换器：支持十进制、十六进制、二进制、八进制
    /// </summary>
    public class IntegerFormatConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2 || values[0] == null || values[1] == null)
                return values[0]?.ToString() ?? "0";

            string valueStr = values[0].ToString() ?? "0";
            string format = values[1].ToString() ?? "Decimal";

            // 尝试解析为整数
            if (!long.TryParse(valueStr, out long number))
            {
                // 如果当前值已经是其他进制，尝试解析
                if (valueStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    if (long.TryParse(valueStr.Substring(2), NumberStyles.HexNumber, culture, out number))
                    {
                        // 成功解析十六进制
                    }
                    else
                    {
                        return valueStr;
                    }
                }
                else if (valueStr.StartsWith("0b", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        number = System.Convert.ToInt64(valueStr.Substring(2), 2);
                    }
                    catch
                    {
                        return valueStr;
                    }
                }
                else
                {
                    return valueStr;
                }
            }

            // 根据格式返回相应的字符串
            return format switch
            {
                "Hexadecimal" => $"0x{number:X}",
                "Binary" => $"0b{System.Convert.ToString(number, 2)}",
                "Octal" => $"0o{System.Convert.ToString(number, 8)}",
                _ => number.ToString()
            };
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            string valueStr = value?.ToString() ?? "0";
            long number = 0;

            // 解析不同进制的输入
            if (valueStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                long.TryParse(valueStr.Substring(2), NumberStyles.HexNumber, culture, out number);
            }
            else if (valueStr.StartsWith("0b", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    number = System.Convert.ToInt64(valueStr.Substring(2), 2);
                }
                catch { }
            }
            else if (valueStr.StartsWith("0o", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    number = System.Convert.ToInt64(valueStr.Substring(2), 8);
                }
                catch { }
            }
            else
            {
                long.TryParse(valueStr, out number);
            }

            return new object[] { number.ToString() };
        }
    }
}