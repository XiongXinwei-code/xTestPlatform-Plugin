using System.Globalization;
using System.Windows.Data;

namespace SerialPort.UI.Converters;

/// <summary>枚举值与 int 之间的转换器（用于 ComboBox SelectedIndex 绑定）</summary>
public class EnumToIntConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null || value == System.Windows.DependencyProperty.UnsetValue)
            return -1;

        return value is Enum e
            ? System.Convert.ToInt32(e, culture)
            : System.Convert.ToInt32(value, culture);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null || value == System.Windows.DependencyProperty.UnsetValue)
            return Binding.DoNothing;

        int index = System.Convert.ToInt32(value, culture);
        if (index < 0) return Binding.DoNothing; // ComboBox 未选中，保持原值

        // 源属性可能是 int（ViewModel 暴露 int），也可能是枚举本身，需分别处理
        var type = Nullable.GetUnderlyingType(targetType) ?? targetType;
        return type.IsEnum ? Enum.ToObject(type, index) : index;
    }
}
