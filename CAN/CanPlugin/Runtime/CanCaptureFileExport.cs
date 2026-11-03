using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace CAN.Runtime;

/// <summary>Only the real file worker releases its path lease, including after a soft timeout.</summary>
internal sealed class CanCaptureFileExport(ICanCaptureFileIo io)
{
    internal static readonly CanCaptureFileExport Default = new(new CanCaptureFileIo());
    private static readonly ConcurrentDictionary<string, byte> ActivePaths = new(StringComparer.OrdinalIgnoreCase);

    internal static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) ||
            path.StartsWith(@"\\?\", StringComparison.Ordinal) || path.StartsWith(@"\\.\", StringComparison.Ordinal))
            throw new ArgumentException("导出路径必须是完整的普通文件路径，不能为相对路径或设备路径");
        var suppliedParts = path[Path.GetPathRoot(path)!.Length..].Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (suppliedParts.Any(part => part.EndsWith('.') || part.EndsWith(' ')))
            throw new ArgumentException("导出路径不能包含尾随点/空格");
        var fullPath = Path.GetFullPath(path);
        if (fullPath.StartsWith(@"\\?\", StringComparison.Ordinal) || fullPath.StartsWith(@"\\.\", StringComparison.Ordinal))
            throw new ArgumentException("导出路径不能为设备路径");
        var relative = fullPath[Path.GetPathRoot(fullPath)!.Length..];
        if (string.IsNullOrEmpty(Path.GetFileName(fullPath)) || relative.Contains(':') ||
            relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(InvalidPart))
            throw new ArgumentException("导出路径不能为目录、备用数据流或包含尾随点/空格");
        return fullPath;
    }

    private static bool InvalidPart(string part)
    {
        var name = part.Split('.')[0].ToUpperInvariant();
        return part.EndsWith('.') || part.EndsWith(' ') || part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            name is "CON" or "PRN" or "AUX" or "NUL" ||
            (name.Length == 4 && (name.StartsWith("COM", StringComparison.Ordinal) || name.StartsWith("LPT", StringComparison.Ordinal)) &&
                (name[3] is >= '0' and <= '9' or '¹' or '²' or '³'));
    }

    internal async Task ExportAsync(string path, string json, bool overwrite, int timeoutMs,
        CancellationToken ct, Action checkLifetime)
    {
        if (timeoutMs <= 0) throw new ArgumentException("导出超时必须大于 0");
        path = NormalizePath(path);
        ct.ThrowIfCancellationRequested(); checkLifetime();
        if (!ActivePaths.TryAdd(path, 0))
            throw new InvalidOperationException($"该文件的上次导出尚未退出，请稍后重试：{path}");
        var started = Stopwatch.GetTimestamp();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(timeoutMs);
        var token = deadline.Token;
        int publishing = 0; // 0 active, 1 commit admitted, 2 cancelled before admission
        using var commitCancellation = token.UnsafeRegister(_ => Interlocked.CompareExchange(ref publishing, 2, 0), null);
        void Check()
        {
            token.ThrowIfCancellationRequested(); checkLifetime();
            if (Stopwatch.GetElapsedTime(started).TotalMilliseconds >= timeoutMs)
                throw new TimeoutException($"CAN 录制导出超过配置期限 {timeoutMs} ms");
        }

        // File open, rename and cleanup may block in the OS. Never execute them on the UI thread.
        var actual = Task.Run(async () =>
        {
            string temporary = Path.Combine(Path.GetDirectoryName(path)!, $".can-capture-{Guid.NewGuid():N}.tmp");
            bool ownsTemporary = false;
            Exception? failure = null;
            try
            {
                Check();
                await using (var stream = io.CreateNew(temporary))
                {
                    ownsTemporary = true;
                    Check();
                    await stream.WriteAsync(Encoding.UTF8.GetBytes(json), token).ConfigureAwait(false);
                    await stream.FlushAsync(token).ConfigureAwait(false);
                }
                Check();
                if (Interlocked.CompareExchange(ref publishing, 1, 0) != 0)
                    throw new OperationCanceledException(token);
                // This is the file commit admission point. A rename admitted first cannot be revoked.
                io.Move(temporary, path, overwrite);
                ownsTemporary = false;
            }
            catch (Exception ex) { failure = ex; throw; }
            finally
            {
                try
                {
                    if (ownsTemporary)
                    {
                        try { io.Delete(temporary); }
                        catch (Exception cleanup)
                        {
                            throw new AggregateException($"CAN 导出临时文件清理失败：{temporary}",
                                failure == null ? [cleanup] : [failure, cleanup]);
                        }
                    }
                }
                finally { ActivePaths.TryRemove(path, out _); }
            }
        }, CancellationToken.None);
        _ = actual.ContinueWith(t =>
        {
            _ = t.Exception;
            Trace.TraceError("CAN 文件导出任务失败；若前台已结束，以下保留真实文件任务与清理异常（不含录制内容）：{0}", t.Exception);
        }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        try
        {
            await actual.WaitAsync(token).ConfigureAwait(false);
            Check();
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested && token.IsCancellationRequested)
        {
            throw new TimeoutException($"CAN 录制导出超过配置期限 {timeoutMs} ms；" +
                (Volatile.Read(ref publishing) != 1 ? "尚未进入文件发布" : "已准入文件发布，目标可能已生成或替换") +
                $"；真实文件任务退出前保留路径占用：{path}");
        }
        catch (TimeoutException ex) when (Volatile.Read(ref publishing) == 1)
        {
            throw new TimeoutException($"CAN 录制导出超过配置期限 {timeoutMs} ms；文件发布已准入，目标可能已生成或替换：{path}", ex);
        }
    }
}

internal interface ICanCaptureFileIo
{
    Stream CreateNew(string path);
    void Move(string source, string destination, bool overwrite);
    void Delete(string path);
}

internal sealed class CanCaptureFileIo : ICanCaptureFileIo
{
    public Stream CreateNew(string path) => new FileStream(path, FileMode.CreateNew, FileAccess.Write,
        FileShare.None, 65536, FileOptions.Asynchronous);
    public void Move(string source, string destination, bool overwrite) => File.Move(source, destination, overwrite);
    public void Delete(string path) => File.Delete(path);
}
