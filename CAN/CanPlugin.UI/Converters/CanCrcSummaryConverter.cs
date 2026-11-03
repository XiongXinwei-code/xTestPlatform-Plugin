using System.Globalization;
using System.Windows.Data;
using CAN.Helpers;
using CAN.Models;

namespace CAN.UI.Converters;

/// <summary>直接读取执行层的算法参数，避免 UI 与实际 CRC 参数表漂移。</summary>
public sealed class CanCrcSummaryConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length == 0 || values[0] is not CanPayloadProtectionSetting setting) return "请选择报文";
        try
        {
            var p = CanPayloadProtection.GetParameters(setting);
            return $"{p.Name}：{p.Width} 位，需要 {p.Width / 8} 个输出字节；Poly=0x{p.Polynomial:X}，Init=0x{p.InitialValue:X}，XorOut=0x{p.XorOutput:X}，RefIn={p.ReflectInput}，RefOut={p.ReflectOutput}。选择算法不会自动更改输出偏移。";
        }
        catch (ArgumentException ex) { return $"CRC 参数无效：{ex.Message}"; }
        catch (FormatException ex) { return $"CRC 参数格式无效：{ex.Message}"; }
        catch (OverflowException ex) { return $"CRC 参数超出范围：{ex.Message}"; }
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException("CRC 参数摘要只读");
}
