using LIN.Models;

namespace LIN.Adapters;

/// <summary>根据适配器类型创建对应的 ILinAdapter 实例</summary>
public static class LinAdapterFactory
{
    public static ILinAdapter Create(LinAdapterType adapterType)
    {
        return adapterType switch
        {
            LinAdapterType.NI     => new NiXnet.NiXnetLinAdapter(),
            // LinAdapterType.PEAK   => new Peak.PeakLinAdapter(),
            // LinAdapterType.Vector => new Vector.VectorLinAdapter(),
            // LinAdapterType.IXXAT  => new Ixxat.IxxatLinAdapter(),
            _ => throw new NotSupportedException($"适配器类型 {adapterType} 尚未实现")
        };
    }
}
