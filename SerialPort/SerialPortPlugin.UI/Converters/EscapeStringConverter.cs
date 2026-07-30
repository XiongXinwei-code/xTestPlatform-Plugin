using System.Globalization;
using System.Windows.Data;

namespace SerialPort.UI.Converters;

/// <summary>
/// 将实际控制字符（\n \r \t）转为转义显示形式，编辑时输入转义形式保存为实际字符。
/// </summary>
public class EscapeStringConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string s) return value;
        return s.Replace("\r\n", "\\r\\n")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string s) return value;
        return s.Replace("\\r\\n", "\r\n")
                .Replace("\\n", "\n")
                .Replace("\\r", "\r")
                .Replace("\\t", "\t");
    }
}
