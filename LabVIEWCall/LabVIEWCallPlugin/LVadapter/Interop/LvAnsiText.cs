using System.Text;

namespace LabVIEWCallPlugin.LVadapter.Interop;

internal static class LvAnsiText
{
    // Windows' active ANSI code page, including UTF-8 when the system selects it.
    private static readonly Encoding WindowsAnsi =
        MakeStrict(CodePagesEncodingProvider.Instance.GetEncoding(0) ?? Encoding.UTF8);

    private static Encoding MakeStrict(Encoding encoding)
    {
        var strict = (Encoding)encoding.Clone();
        strict.EncoderFallback = EncoderFallback.ExceptionFallback;
        strict.DecoderFallback = DecoderFallback.ExceptionFallback;
        return strict;
    }

    internal static string Decode(byte[] source, int length) => WindowsAnsi.GetString(source, 0, length);

    internal static void Check(string value, string name, Encoding? testEncoding = null)
    {
        ArgumentNullException.ThrowIfNull(value, name);
        if (value.Contains('\0'))
            throw new ArgumentException("VI 桥接字符串含有空字符，目标名称会被截断。", name);
        Encoding encoding = testEncoding == null ? WindowsAnsi : MakeStrict(testEncoding);
        try
        {
            if (string.Equals(value, encoding.GetString(encoding.GetBytes(value)), StringComparison.Ordinal))
                return;
        }
        catch (EncoderFallbackException ex)
        {
            throw new ArgumentException($"VI 桥接字符串不能用系统代码页 {encoding.CodePage} 精确表示。", name, ex);
        }
        throw new ArgumentException($"VI 桥接字符串在代码页 {encoding.CodePage} 往返转换后发生变化。", name);
    }
}
