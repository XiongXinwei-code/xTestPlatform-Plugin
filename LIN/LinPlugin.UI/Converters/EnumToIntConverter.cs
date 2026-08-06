using System.Globalization;
using System.Windows.Data;

namespace LIN.UI.Converters;

/// <summary>枚举整数与 ComboBox SelectedIndex 互转</summary>
public class EnumToIntConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => (int)value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Enum.ToObject(targetType, (int)value);
}
