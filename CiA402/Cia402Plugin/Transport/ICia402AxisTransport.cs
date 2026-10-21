using CiA402.Models;

namespace CiA402.Transport;

public sealed record AxisEndpoint(string ConnectionName, int Address, BindAxisSetting Configuration);
public sealed record AxisFeedback(ushort StatusWord, int? Position, int? Velocity, short? Torque);

/// <summary>主站实现必须保证单次 I/O 有界、取消可终止提交，不拥有共享主站生命周期。</summary>
public interface ICia402AxisTransport : IDisposable
{
    /// <summary>允许在控制流程的单次事务之间独立读取；不表示底层 I/O 可并行。默认拒绝，后端须显式声明。</summary>
    bool SupportsConcurrentMonitoring => false;
    Task<byte[]> ReadObjectAsync(ushort index, byte subIndex, CancellationToken ct);
    Task WriteObjectAsync(ushort index, byte subIndex, byte[] value, CancellationToken ct);
    Task WriteTargetAsync(OperationMode mode, long target, ushort controlWord, CancellationToken ct);
    Task<AxisFeedback> ReadFeedbackAsync(bool position, bool velocity, bool torque, CancellationToken ct);
    /// <summary>仅检查不可变配置，不执行 I/O。</summary>
    void RequireCyclicMode(OperationMode mode);
}

/// <summary>以 ResourceKey 注册到执行上下文资源表；主站适配器自行管理从站控制权和过程映像。</summary>
public interface ICia402EtherCatFactory
{
    public const string ResourceKey = "CiA402.EtherCAT.Factory";
    Task<ICia402AxisTransport> OpenAsync(AxisEndpoint endpoint, CancellationToken ct);
}
