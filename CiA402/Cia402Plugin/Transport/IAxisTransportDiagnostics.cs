namespace CiA402.Transport;

/// <summary>仅返回本地缓存的诊断文字，不能执行 I/O 或等待锁外设备调用。</summary>
internal interface IAxisTransportDiagnostics
{
    string DiagnosticContext { get; }
}
