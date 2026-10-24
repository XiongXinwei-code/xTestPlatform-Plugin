using System.Globalization;
using CAN.Adapters.Peak;
using CAN.Adapters.Zlg;
using CAN.Models;

namespace CAN.Runtime;

/// <summary>按现有适配器解析规则归一化通道别名；不猜测驱动配置中的设备别名。</summary>
public static class CanChannelIdentity
{
    public static string Normalize(CanAdapterType type, string channel)
    {
        var value = channel.Trim();
        switch (type)
        {
            case CanAdapterType.PEAK:
                value = PcanApi.ParseChannel(value).ToString(CultureInfo.InvariantCulture);
                break;
            case CanAdapterType.ZLG:
                var parts = value.Split('/');
                if (parts.Length != 3 || !uint.TryParse(parts[1], out var device) || !uint.TryParse(parts[2], out var port))
                    throw new ArgumentException("ZLG 通道应为设备类型/设备索引/通道索引");
                value = $"{ZlgApi.ParseDeviceType(parts[0])}/{device}/{port}";
                break;
            case CanAdapterType.Vector:
            case CanAdapterType.Kvaser:
            case CanAdapterType.TOSUN:
                if (!int.TryParse(value, out var index) || index < 0 || (type == CanAdapterType.Vector && index > 63))
                    throw new ArgumentException("CAN 通道索引无效");
                value = index.ToString(CultureInfo.InvariantCulture);
                break;
        }
        return $"{type}:{value.ToUpperInvariant()}";
    }
}
