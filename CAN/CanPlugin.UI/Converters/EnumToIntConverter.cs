using System.Globalization;
using System.Windows.Data;

namespace CAN.UI.Converters;

/// <summary>枚举值与 int 之间的转换器（用于 ComboBox SelectedIndex 绑定）</summary>
public class EnumToIntConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => (int)value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Enum.ToObject(targetType, (int)value);
}
