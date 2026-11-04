using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;

namespace CAN.Dbc;

/// <summary>DBC 数字文本的统一入口，拒绝 decimal 解析中的隐式舍入。</summary>
public static class DbcNumber
{
    public const int MaximumTextCharacters = 128;
    private static readonly Regex Syntax = new(@"^([+-]?)(\d*)(?:\.(\d*))?(?:[eE]([+-]?\d+))?$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, TimeSpan.FromMilliseconds(200));

    public static decimal ParseExact(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length > MaximumTextCharacters) throw new FormatException("DBC 数值文本不能超过 128 个字符");
        text = text.Trim();
        if (!decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal value))
            throw new FormatException("DBC 数值格式无效或超出 decimal 范围");
        // Decimal.TryParse can silently round an over-precise value. Compare the
        // original base-ten rational with the parsed decimal before accepting it.
        var exact = Syntax.Match(text);
        if (!exact.Success) throw new FormatException("DBC 数值格式无效");
        string digits = (exact.Groups[2].Value + exact.Groups[3].Value).TrimStart('0');
        if (digits.Length == 0) return value;
        if (!int.TryParse(exact.Groups[4].Success ? exact.Groups[4].Value : "0", NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int exponent))
            throw new FormatException("DBC 数值指数超出范围");
        long scale = (long)exact.Groups[3].Length - exponent;
        while (digits.EndsWith('0')) { digits = digits[..^1]; scale--; }
        if (scale > 28 || scale < -28) throw new FormatException("DBC 数值无法精确表示为 decimal，未执行舍入");
        BigInteger source = BigInteger.Parse(digits, CultureInfo.InvariantCulture);
        if (scale < 0) { source *= BigInteger.Pow(10, (int)-scale); scale = 0; }
        if (exact.Groups[1].Value == "-") source = -source;
        int[] bits = decimal.GetBits(value);
        BigInteger parsed = (BigInteger)(uint)bits[0] | ((BigInteger)(uint)bits[1] << 32) | ((BigInteger)(uint)bits[2] << 64);
        if (bits[3] < 0) parsed = -parsed;
        int parsedScale = (bits[3] >> 16) & 0xFF;
        if (source * BigInteger.Pow(10, parsedScale) != parsed * BigInteger.Pow(10, (int)scale))
            throw new FormatException("DBC 数值无法精确表示为 decimal，未执行舍入");
        return value;
    }
}
