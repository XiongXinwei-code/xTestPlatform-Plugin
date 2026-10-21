using CiA402.Models;

namespace CiA402.Transport;

public sealed record AxisEndpoint(string ConnectionName, int Address, BindAxisSetting Configuration);
public sealed record AxisFeedback(ushort StatusWord, int? Position, int? Velocity, short? Torque);

/// <summary>主站实现必须保证单次 I/O 有界、取消可终止提交，不拥有共享主站生命周期。</summary>
public interface ICia402AxisTransport : IDisposable
{
    Task<byte[]> ReadObjectAsync(ushort index, byte subIndex, CancellationToken ct);
    Task WriteObjectAsync(ushort index, byte subIndex, byte[] value, CancellationToken ct);
    Task WriteTargetAsync(OperationMode mode, long target, ushort controlWord, CancellationToken ct);
    Task<AxisFeedback> ReadFeedbackAsync(bool position, bool velocity, bool torque, CancellationToken ct);
    void RequireCyclicMode(OperationMode mode);
}

/// <summary>以 ResourceKey 注册到执行上下文资源表；主站适配器自行管理从站控制权和过程映像。</summary>
public interface ICia402EtherCatFactory
{
    public const string ResourceKey = "CiA402.EtherCAT.Factory";
    Task<ICia402AxisTransport> OpenAsync(AxisEndpoint endpoint, CancellationToken ct);
}
