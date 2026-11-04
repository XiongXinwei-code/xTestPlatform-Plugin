using System.IO;
using System.Buffers;
using System.Security.Cryptography;
using System.Text;

namespace CAN.Dbc;

public static class DbcDocumentIO
{
    public const int MaximumBytes = 8 * 1024 * 1024;
    private static readonly DbcConcurrentWork<(string Path, string Encoding), DbcDocumentSetting> FileReads = new(4);

    public static string ValidateFileSource(string path, string encodingName)
    {
        if (encodingName is not ("Auto" or "UTF-8" or "GBK" or "GB2312" or "Latin1"))
            throw new ArgumentException("DBC 文件编码必须为 Auto、UTF-8、GBK、GB2312 或 Latin1");
        path = path?.Trim() ?? "";
        if (string.IsNullOrEmpty(path) || !Path.IsPathFullyQualified(path) || path.Contains("://", StringComparison.Ordinal)
            || path.StartsWith(@"\\.\", StringComparison.Ordinal) || path.StartsWith(@"\\?\", StringComparison.Ordinal)
            || path.StartsWith(@"\??\", StringComparison.Ordinal))
            throw new ArgumentException("DbcFilePath 必须为绝对 .dbc 文件路径（不加表达式引号），不支持相对路径、URL 或设备路径");
        string full = Path.GetFullPath(path);
        // Windows canonicalizes forward slashes too: //./pipe/x.dbc is a device path.
        if (full.StartsWith(@"\\.\", StringComparison.Ordinal) || full.StartsWith(@"\\?\", StringComparison.Ordinal)
            || full.StartsWith(@"\??\", StringComparison.Ordinal))
            throw new ArgumentException("DbcFilePath 不支持设备路径，只允许普通 .dbc 文件路径");
        string root = Path.GetPathRoot(full) ?? "";
        foreach (string component in full[root.Length..].Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            string device = component.Split('.')[0].TrimEnd(' ', '.').ToUpperInvariant();
            bool numberedDevice = device.Length == 4 && (device.StartsWith("COM", StringComparison.Ordinal) || device.StartsWith("LPT", StringComparison.Ordinal))
                && "123456789¹²³".Contains(device[3]);
            if (device is "CON" or "PRN" or "AUX" or "NUL" or "CONIN$" or "CONOUT$" || numberedDevice)
                throw new ArgumentException("DbcFilePath 不支持 DOS 保留设备名，只允许普通 .dbc 文件路径");
        }
        if (!string.Equals(Path.GetExtension(full), ".dbc", StringComparison.OrdinalIgnoreCase)
            || full.AsSpan(root.Length).Contains(':'))
            throw new ArgumentException("DbcFilePath 必须指向普通 .dbc 文件，不支持其他扩展名或数据流路径");
        return full;
    }

    public static Task<DbcDocumentSetting> ResolveAsync(DbcSourceSetting setting, CancellationToken ct = default)
        => ResolveAsync(setting, ReadAsync, ct);

    internal static async Task<DbcDocumentSetting> ResolveAsync(DbcSourceSetting setting,
        Func<string, string, CancellationToken, Task<DbcDocumentSetting>> read, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(setting);
        ct.ThrowIfCancellationRequested();
        string path = setting.DbcFilePath;
        string encoding = setting.DbcFileEncoding;
        if (string.IsNullOrWhiteSpace(path))
        {
            var source = setting.Document;
            if (source == null || string.IsNullOrWhiteSpace(source.Text))
                throw new ArgumentException("请填写 DbcFilePath 绝对文件路径，或选择 DBC 文件并导入内嵌快照");
            return new() { Name = source.Name, Text = source.Text, Encoding = source.Encoding };
        }
        path = ValidateFileSource(path, encoding);
        // Opening a file (including a network share) can outlive cancellation.
        // Only this read owns the stream; late completion cannot publish a catalog/settings.
        var reading = read(path, encoding, ct);
        try
        {
            var document = await reading.WaitAsync(ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            return document;
        }
        catch
        {
            _ = reading.ContinueWith(t => { _ = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
            throw;
        }
    }

    public static async Task<DbcDocumentSetting> ReadAsync(string path, string encodingName, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        path = ValidateFileSource(path, encodingName);
        var source = await FileReads.RunAsync((path, encodingName), token => ReadFileAsync(path, encodingName, token), ct)
            .ConfigureAwait(false);
        // Share immutable text, never another step's writable Document setting.
        return new() { Name = source.Name, Text = source.Text, Encoding = source.Encoding };
    }

    private static async Task<DbcDocumentSetting> ReadFileAsync(string path, string encodingName, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true);
        long length = stream.Length;
        if (length > MaximumBytes) throw new ArgumentException("DBC 文件不能超过 8 MiB");
        using var memory = new MemoryStream((int)length);
        var buffer = new byte[65536];
        int count;
        while ((count = await stream.ReadAsync(buffer, ct).ConfigureAwait(false)) != 0)
        {
            if (memory.Length + count > MaximumBytes) throw new ArgumentException("DBC 文件读取期间超过 8 MiB");
            memory.Write(buffer, 0, count);
        }
        var decoded = Decode(memory.GetBuffer().AsSpan(0, (int)memory.Length), encodingName);
        ct.ThrowIfCancellationRequested();
        if (decoded.Text.Length > MaximumBytes || Encoding.UTF8.GetByteCount(decoded.Text) > MaximumBytes)
            throw new ArgumentException("DBC 解码文本不能超过 8 MiB UTF-8");
        return new DbcDocumentSetting { Name = Path.GetFileName(path), Text = decoded.Text, Encoding = decoded.Encoding };
    }

    internal static (string Text, string Encoding) Decode(ReadOnlySpan<byte> bytes, string encodingName)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Encoding Selected(string name) => name switch
        {
            "UTF-8" => new UTF8Encoding(false, true),
            "GBK" or "GB2312" => Encoding.GetEncoding(936, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback),
            "Latin1" => Encoding.Latin1,
            _ => throw new ArgumentException("DBC 编码必须为 Auto、UTF-8、GBK、GB2312 或 Latin1")
        };
        string actual = encodingName;
        string text;
        if (encodingName == "Auto")
        {
            try { text = Selected("UTF-8").GetString(bytes); actual = "UTF-8"; }
            catch (DecoderFallbackException) { text = Selected("GBK").GetString(bytes); actual = "GBK"; }
        }
        else text = Selected(encodingName).GetString(bytes);
        return (text.TrimStart('\uFEFF'), actual);
    }

    public static string Fingerprint(DbcDocumentSetting document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return FingerprintText(document.Text, CancellationToken.None);
    }

    internal static string FingerprintText(string text, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("尚未选择并加载 DBC 文件");
        if (text.Length > MaximumBytes) throw new ArgumentException("内嵌 DBC 文本不能超过 8 MiB UTF-8");
        const int chunk = 4096;
        var utf8 = new UTF8Encoding(false, true);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(utf8.GetMaxByteCount(chunk));
        try
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            int total = 0;
            for (int offset = 0; offset < text.Length;)
            {
                ct.ThrowIfCancellationRequested();
                int chars = Math.Min(chunk, text.Length - offset);
                if (offset + chars < text.Length && char.IsHighSurrogate(text[offset + chars - 1])) chars--;
                int bytes = utf8.GetBytes(text.AsSpan(offset, chars), buffer);
                total += bytes;
                if (total > MaximumBytes) throw new ArgumentException("内嵌 DBC 文本不能超过 8 MiB UTF-8");
                hash.AppendData(buffer.AsSpan(0, bytes));
                offset += chars;
            }
            return Convert.ToHexString(hash.GetHashAndReset());
        }
        finally { ArrayPool<byte>.Shared.Return(buffer); }
    }
}
