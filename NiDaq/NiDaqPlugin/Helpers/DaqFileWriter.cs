using System.Globalization;
using System.Text;

namespace NiDaq.Helpers;

/// <summary>数据存盘辅助：文件轮转 + CSV 追加写入</summary>
internal static class DaqFileWriter
{
    /// <summary>
    /// 将 double[,] 数据追加写入 CSV 文件。
    /// 文件不存在则创建；超过指定大小则重命名旧文件并新建。
    /// </summary>
    public static void AppendCsv(string filePath, double[,] data, string[]? channelNames, int maxSizeMB, Action<string>? log)
    {
        EnsureDirectory(filePath);
        RotateIfNeeded(filePath, maxSizeMB, log);

        bool writeHeader = !File.Exists(filePath) || new FileInfo(filePath).Length == 0;
        int channels = data.GetLength(0);
        int samples = data.GetLength(1);

        using var writer = new StreamWriter(filePath, append: true, new UTF8Encoding(false));

        if (writeHeader && channelNames != null && channelNames.Length == channels)
        {
            writer.WriteLine(string.Join(",", channelNames));
        }

        for (int s = 0; s < samples; s++)
        {
            var sb = new StringBuilder();
            for (int ch = 0; ch < channels; ch++)
            {
                if (ch > 0) sb.Append(',');
                sb.Append(data[ch, s].ToString("G", CultureInfo.InvariantCulture));
            }
            writer.WriteLine(sb.ToString());
        }
    }

    /// <summary>
    /// 同步采集存盘：将编码器位置与 AI 模拟量数据合并写入同一 CSV。
    /// </summary>
    public static void AppendSyncCsv(string filePath, double[,] aiData, string[] channelNames, double encoderValue, int maxSizeMB, Action<string>? log)
    {
        EnsureDirectory(filePath);
        RotateIfNeeded(filePath, maxSizeMB, log);

        int channels = aiData.GetLength(0);
        int samples = aiData.GetLength(1);
        bool writeHeader = !File.Exists(filePath) || new FileInfo(filePath).Length == 0;

        using var writer = new StreamWriter(filePath, append: true, new UTF8Encoding(false));

        if (writeHeader)
        {
            writer.WriteLine("Encoder," + string.Join(",", channelNames));
        }

        for (int s = 0; s < samples; s++)
        {
            var sb = new StringBuilder();
            sb.Append(encoderValue.ToString("G", CultureInfo.InvariantCulture));
            for (int ch = 0; ch < channels; ch++)
            {
                sb.Append(',');
                sb.Append(aiData[ch, s].ToString("G", CultureInfo.InvariantCulture));
            }
            writer.WriteLine(sb.ToString());
        }
    }

    /// <summary>构建输出文件完整路径</summary>
    public static string BuildFilePath(string outputDirectory, string baseName, string extension)
    {
        var dir = string.IsNullOrWhiteSpace(outputDirectory) ? Path.GetTempPath() : outputDirectory;
        return Path.Combine(dir, $"{baseName}.{extension}");
    }

    private static void EnsureDirectory(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
    }

    private static void RotateIfNeeded(string filePath, int maxSizeMB, Action<string>? log)
    {
        if (!File.Exists(filePath)) return;

        long maxBytes = (long)maxSizeMB * 1024 * 1024;
        var fi = new FileInfo(filePath);
        if (fi.Length < maxBytes) return;

        log?.Invoke($"数据文件已超过 {maxSizeMB}MB ({fi.Length / (1024 * 1024)} MB)，执行文件轮转: {filePath}");

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var dir = Path.GetDirectoryName(filePath)!;
        var nameNoExt = Path.GetFileNameWithoutExtension(filePath);
        var ext = Path.GetExtension(filePath);
        var newName = Path.Combine(dir, $"{nameNoExt}_{timestamp}{ext}");
        File.Move(filePath, newName);
    }
}
