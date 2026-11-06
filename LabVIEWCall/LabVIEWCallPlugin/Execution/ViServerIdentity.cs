namespace LabVIEWCallPlugin.Execution;

/// <summary>Immutable identity of the IDE owning the local VI Server endpoint.</summary>
internal sealed record ViServerIdentity(int Port, int ProcessId, DateTime StartTimeUtc,
    string ImagePath, string FileVersion, ushort Machine)
{
    internal string Diagnostic => $"VI Server=127.0.0.1:{Port}；PID={ProcessId}；启动时间={StartTimeUtc:O}；"
        + $"进程={ImagePath}；版本={FileVersion}；机器类型=0x{Machine:X4}";

    internal void Validate()
    {
        if (Port is < 1 or > ushort.MaxValue || ProcessId <= 0 || StartTimeUtc.Kind != DateTimeKind.Utc || !Path.IsPathFullyQualified(ImagePath))
            throw new InvalidOperationException($"VI Server 进程身份不完整，已拒绝连接；{Diagnostic}。");
        if (!Path.GetFileName(ImagePath).Equals("LabVIEW.exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"端口 {Port} 被非 LabVIEW 开发环境占用，已拒绝连接；{Diagnostic}。");
        // VI Server is an IPC boundary. The target IDE need not share the x64
        // worker's architecture; retain Machine as identity/diagnostic information.
    }

    internal void EnsureSame(ViServerIdentity current)
    {
        Validate();
        current.Validate();
        if (Port != current.Port || ProcessId != current.ProcessId || StartTimeUtc != current.StartTimeUtc ||
            !ImagePath.Equals(current.ImagePath, StringComparison.OrdinalIgnoreCase) ||
            FileVersion != current.FileVersion || Machine != current.Machine)
            throw new InvalidOperationException($"VI Server 实例已变更，禁止沿用旧 VI 引用或接受旧结果。原实例：{Diagnostic}；当前实例：{current.Diagnostic}。");
    }
}
