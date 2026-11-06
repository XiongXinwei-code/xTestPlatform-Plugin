using System.Xml;
using System.Xml.Linq;

namespace ZlgEtherCat.Runtime;

public sealed record EniConfiguration(int CycleUs, bool HasDc);
public static class EniValidation
{
    public static EniConfiguration Read(Stream stream, int expectedCycleUs, bool requireDc)
    {
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 64 * 1024 * 1024, CloseInput = false });
        var doc = XDocument.Load(reader);
        var cycles = doc.Descendants().Where(n => n.Name.LocalName == "CycleTime").Select(n => int.Parse(n.Value, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        if (cycles.Length == 0 || cycles[0] != expectedCycleUs) throw new ArgumentException($"ENI 主周期必须等于配置值 {expectedCycleUs} μs");
        var dc = doc.Descendants().Where(n => n.Name.LocalName == "DC").ToArray();
        bool hasDc = dc.Any(n => n.Descendants().Any(v => v.Name.LocalName == "CycleTime0" && long.TryParse(v.Value, out long cycle) && cycle > 0));
        if (requireDc && !hasDc) throw new ArgumentException("ENI 未包含有效的 DC CycleTime0 配置");
        if (requireDc && dc.SelectMany(n => n.Descendants()).Where(n => n.Name.LocalName == "CycleTime0").Any(n => long.TryParse(n.Value, out long v) && v > 0 && v % (expectedCycleUs * 1000L) != 0))
            throw new ArgumentException("ENI DC 同步周期必须与主周期一致或为其整数倍");
        return new(expectedCycleUs, hasDc);
    }
}
