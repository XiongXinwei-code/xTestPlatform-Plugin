using System.Globalization;
using Opc.Ua;
using OpcUa.Models;
using xTestPlatform.Core.Models;

namespace OpcUa.Helpers;

public static class OpcUaDataAcqOutput
{
    /// <summary>兼容 Waveform 输出只表示记录序号，不伪造等间隔的采集时间。</summary>
    public static WaveformData CreateWaveform(string taskName, IReadOnlyList<OpcUaDataAcqItem> items, IReadOnlyList<DataAcqRecord> records)
    {
        var waveform = new WaveformData
        {
            TaskID = taskName, SampleRate = 1, X0 = records.Count > 0 ? records[0].Sequence : 0,
            XUnit = "sample", StartTime = records.Count > 0 ? records[0].Timestamp : DateTime.UtcNow,
            Channels = new List<ChannelData>(items.Count)
        };
        for (int i = 0; i < items.Count; i++)
        {
            var data = new double[records.Count];
            for (int n = 0; n < records.Count; n++)
            {
                var record = records[n];
                if (i >= record.Values.Length) throw new InvalidOperationException("采集记录列数与通道数不一致");
                data[n] = i >= record.StatusCodes.Length || !StatusCode.IsGood(record.StatusCodes[i]) || record.Values[i] == null
                    ? double.NaN : Convert.ToDouble(record.Values[i], CultureInfo.InvariantCulture);
            }
            waveform.Channels.Add(new ChannelData
            { Channel = string.IsNullOrWhiteSpace(items[i].ColumnName) ? items[i].NodeId : items[i].ColumnName, Values = data });
        }
        return waveform;
    }
}
