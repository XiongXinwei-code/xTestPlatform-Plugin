using System;
using System.Globalization;
using System.Windows.Data;

namespace LabVIEWCallPlugin.UI.Converters
{
    /// <summary>
    /// 布尔值与字符串互转：T = true, F = false
    /// </summary>
    public class BooleanStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string strValue)
            {
                return strValue.Equals("T", StringComparison.OrdinalIgnoreCase) || 
                       strValue.Equals("True", StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? "T" : "F";
            }
            return "F";
        }
    }
}