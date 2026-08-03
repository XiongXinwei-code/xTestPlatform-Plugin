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
            // CanAdapterType.PEAK => new PcanAdapter(),
            // CanAdapterType.Vector => new VectorAdapter(),
            // CanAdapterType.ZLG => new ZlgAdapter(),
            _ => throw new NotSupportedException($"适配器类型 {adapterType} 尚未实现")
        };
    }
}
