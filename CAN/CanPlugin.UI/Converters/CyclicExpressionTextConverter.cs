using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CAN.UI.Converters;

/// <summary>UI 显示表达式或常量；编辑只写真实表达式字段，不在 Setting 暴露不可持久化别名。</summary>
public sealed class CyclicExpressionTextConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length != 2 || values.Any(value => value == DependencyProperty.UnsetValue)) return "";
        if (values[0] is string expression && !string.IsNullOrWhiteSpace(expression)) return expression;
        return values[1] is bool enabled ? enabled ? "true" : "false"
            : System.Convert.ToString(values[1], CultureInfo.InvariantCulture) ?? "";
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        [value as string ?? "", Binding.DoNothing];
}
