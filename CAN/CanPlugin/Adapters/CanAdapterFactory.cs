using CAN.Models;

namespace CAN.Adapters;

/// <summary>根据适配器类型创建对应的 ICanAdapter 实例</summary>
public static class CanAdapterFactory
{
    public static ICanAdapter Create(CanAdapterType adapterType)
    {
        return adapterType switch
        {
            CanAdapterType.NI => new NiXnet.NiXnetAdapter(),
            CanAdapterType.PEAK => new Peak.PcanAdapter(),
            CanAdapterType.Vector => new Vector.VectorAdapter(),
            CanAdapterType.ZLG => new Zlg.ZlgAdapter(),
            CanAdapterType.Kvaser => new Kvaser.KvaserAdapter(),
            CanAdapterType.TOSUN => new Tosun.TosunAdapter(),
            _ => throw new NotSupportedException($"适配器类型 {adapterType} 尚未实现")
        };
    }
}
