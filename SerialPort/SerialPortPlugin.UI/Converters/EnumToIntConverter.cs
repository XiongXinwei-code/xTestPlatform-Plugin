using System.Globalization;
using System.Windows.Data;

namespace SerialPort.UI.Converters;

public class EnumToIntConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is null ? -1 : System.Convert.ToInt32(value, culture);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null) return Binding.DoNothing;

        int index = System.Convert.ToInt32(value, culture);
        // ComboBox 无选中项时 SelectedIndex 为 -1，不回写以免产生非法枚举值
        if (index < 0) return Binding.DoNothing;

        // 绑定目标可能是 int 属性，也可能是枚举属性，需分别处理
        var type = Nullable.GetUnderlyingType(targetType) ?? targetType;
        return type.IsEnum ? Enum.ToObject(type, index) : System.Convert.ChangeType(index, type, culture);
    }
}
