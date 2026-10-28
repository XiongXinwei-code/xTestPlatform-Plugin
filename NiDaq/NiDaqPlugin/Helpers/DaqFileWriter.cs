using System.Globalization;
using System.Text;
using System.Collections.Concurrent;

namespace NiDaq.Helpers;

/// <summary>数据存盘辅助：文件轮转 + CSV 追加写入</summary>
internal static class DaqFileWriter
{
    private static readonly ConcurrentDictionary<string, object> FileGates = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>
    /// 将 double[,] 数据追加写入 CSV 文件。
    /// 文件不存在则创建；超过指定大小则重命名旧文件并新建。
    /// </summary>
    public static void AppendCsv(string filePath, double[,] data, string[]? channelNames, int maxSizeMB, Action<string>? log,
        Action? ensureActive = null)
    {
        if (maxSizeMB <= 0) throw new ArgumentOutOfRangeException(nameof(maxSizeMB));
        if (channelNames == null || channelNames.Length != data.GetLength(0)) throw new ArgumentException("通道列名必须与数据维度一致");
        filePath = Path.GetFullPath(filePath);
        object fileGate = FileGates.GetOrAdd(filePath, static _ => new object());
        bool entered = false;
        try
        {
        // Waiting for another file transaction must not start a late export after its
        // owner has timed out/closed. The actual lock is held until real file I/O exits.
        ensureActive?.Invoke();
        while (!(entered = Monitor.TryEnter(fileGate, 25))) ensureActive?.Invoke();
        ensureActive?.Invoke();
        EnsureDirectory(filePath, ensureActive);
        ensureActive?.Invoke();
        string expectedHeader = string.Join(",", channelNames.Select(EscapeColumn));
        if (File.Exists(filePath) && new FileInfo(filePath).Length > 0)
        {
            using var existing = new StreamReader(filePath, Encoding.UTF8, true);
            if (existing.ReadLine() != expectedHeader) throw new InvalidDataException("现有 CSV 的通道表头不同，拒绝混写数据");
        }
        ensureActive?.Invoke();
        RotateIfNeeded(filePath, maxSizeMB, log, ensureActive);

        bool writeHeader = !File.Exists(filePath) || new FileInfo(filePath).Length == 0;
        int channels = data.GetLength(0);
        int samples = data.GetLength(1);

        ensureActive?.Invoke();
        using var writer = new StreamWriter(filePath, append: true, new UTF8Encoding(false));

        if (writeHeader && channelNames != null && channelNames.Length == channels)
        {
            ensureActive?.Invoke();
            writer.WriteLine(expectedHeader);
        }

        for (int s = 0; s < samples; s++)
        {
            ensureActive?.Invoke();
            var sb = new StringBuilder();
            for (int ch = 0; ch < channels; ch++)
            {
                if (ch > 0) sb.Append(',');
                sb.Append(data[ch, s].ToString("G", CultureInfo.InvariantCulture));
            }
            ensureActive?.Invoke();
            writer.WriteLine(sb.ToString());
        }
        ensureActive?.Invoke();
        writer.Flush();
        ensureActive?.Invoke();
        if (writer.BaseStream is FileStream file) file.Flush(flushToDisk: true);
        }
        finally { if (entered) Monitor.Exit(fileGate); }
    }
    private static string EscapeColumn(string value)
    {
        if (value.IndexOfAny(['\r', '\n']) >= 0) throw new ArgumentException("CSV 通道名称不能包含换行");
        return value.IndexOfAny([',', '"']) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
    }

    /// <summary>
    /// 同步采集存盘：将编码器位置与 AI 模拟量数据合并写入同一 CSV。
    /// </summary>
    public static void AppendSyncCsv(string filePath, double[,] aiData, string[] channelNames, double encoderValue, int maxSizeMB, Action<string>? log)
        => throw new NotSupportedException("编码器标量不能复制为同步采样点；请将逐点 AI/CI 矩阵交给 AppendCsv");

    /// <summary>构建输出文件完整路径</summary>
    public static string BuildFilePath(string outputDirectory, string baseName, string extension)
    {
        if (string.IsNullOrWhiteSpace(baseName) || baseName is "." or ".." || baseName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || baseName.Contains('/') || baseName.Contains('\\'))
            throw new ArgumentException("任务名称不能包含路径或非法文件名字符", nameof(baseName));
        if (!string.Equals(extension, "csv", StringComparison.OrdinalIgnoreCase)) throw new NotSupportedException("当前数据写入仅支持 CSV");
        var dir = string.IsNullOrWhiteSpace(outputDirectory) ? Path.GetTempPath() : outputDirectory;
        return Path.GetFullPath(Path.Combine(dir, $"{baseName}.{extension}"));
    }

    private static void EnsureDirectory(string filePath, Action? ensureActive)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            ensureActive?.Invoke();
            Directory.CreateDirectory(dir);
        }
    }

    private static void RotateIfNeeded(string filePath, int maxSizeMB, Action<string>? log, Action? ensureActive)
    {
        if (!File.Exists(filePath)) return;

        long maxBytes = (long)maxSizeMB * 1024 * 1024;
        var fi = new FileInfo(filePath);
        if (fi.Length < maxBytes) return;

        log?.Invoke($"数据文件已超过 {maxSizeMB}MB ({fi.Length / (1024 * 1024)} MB)，执行文件轮转: {filePath}");
        ensureActive?.Invoke();

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fffffff") + "_" + Guid.NewGuid().ToString("N");
        var dir = Path.GetDirectoryName(filePath)!;
        var nameNoExt = Path.GetFileNameWithoutExtension(filePath);
        var ext = Path.GetExtension(filePath);
        var newName = Path.Combine(dir, $"{nameNoExt}_{timestamp}{ext}");
        ensureActive?.Invoke();
        File.Move(filePath, newName);
    }
}
