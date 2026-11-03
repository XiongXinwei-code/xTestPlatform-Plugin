using Google.Protobuf;
using Daq = NationalInstruments.Grpc.NiDAQmx;

namespace NiGrpc.Runtime;

/// <summary>新增 DAQmx 操作的唯一参数契约；执行器和编辑器共享校验，不调用设备。</summary>
internal static class DaqmxExtendedRequests
{
    internal const int MaximumSamples = 1_000_000;
    private static readonly Dictionary<string, string[]> Oneofs = new(StringComparer.Ordinal)
    {
        ["CreateDIChan"] = ["line_grouping_enum"], ["CreateDOChan"] = ["line_grouping_enum"],
        ["ReadDigitalU32"] = ["fill_mode_enum"], ["WriteDigitalU32"] = ["data_layout_enum"],
        ["CreateCICountEdgesChan"] = ["edge_enum", "count_direction_enum"],
        ["CreateCIFreqChan"] = ["units_enum", "edge_enum", "meas_method_enum"],
        ["CreateCOPulseChanFreq"] = ["units_enum", "idle_state_enum"],
        ["ReadCounterU32Ex"] = ["fill_mode_enum"], ["ReadCounterF64Ex"] = ["fill_mode_enum"],
        ["CreateCIAngEncoderChan"] = ["decoding_type_enum", "zidx_phase_enum", "units_enum"],
        ["CreateCILinEncoderChan"] = ["decoding_type_enum", "zidx_phase_enum", "units_enum"],
        ["CfgImplicitTiming"] = ["sample_mode_enum"], ["CfgDigEdgeStartTrig"] = ["trigger_edge_enum"],
        ["ExportSignal"] = ["signal_id_enum"], ["SetTimingAttributeString"] = ["attribute_enum"],
        ["SetTimingAttributeDouble"] = ["attribute_enum"], ["WriteCtrFreq"] = ["data_layout_enum"]
    };
    internal static bool Supports(string? method) => method != null && Oneofs.ContainsKey(method);

    internal static string Describe(string method) => method switch
    {
        "CreateDIChan" or "CreateDOChan" => "lines 为物理端口/线字符串，可选 nameToAssignToLines；lineGrouping 或 lineGroupingRaw 必填：LINE_GROUPING_CHAN_PER_LINE（0，每线一个虚拟通道）或 LINE_GROUPING_CHAN_FOR_ALL_LINES（1，所选线合并为一个虚拟通道）。U32 读写每通道样本表示该通道的数字位图，最多 32 位；不是每个物理线都占一个数组元素。端口 U32 保留物理端口位位置：port0/line4:7 全部置高是 0xF0，不压缩成 0x0F",
        "ReadDigitalU32" or "ReadCounterU32Ex" or "ReadCounterF64Ex" => "numSampsPerChan 为 -1（当前可用）或正整数；timeout 为有限非负秒数；fillMode 或 fillModeRaw 必填：GROUP_BY_GROUP_BY_CHANNEL（0，通道优先）或 GROUP_BY_GROUP_BY_SCAN_NUMBER（1，扫描优先）；arraySizeInSamps 为总数组容量 1～1000000，须容纳通道数×所请求采样数。响应 readArray 为样本数组，sampsPerChanRead 是实际每通道样本数。CounterU32 用于边沿计数，CounterF64 用于频率/编码器物理位置，单位由配置步骤决定",
        "WriteDigitalU32" => "numSampsPerChan 为正整数，autoStart 为 bool（默认 false）；timeout 为有限非负秒数；dataLayout 或 dataLayoutRaw 必填：GROUP_BY_GROUP_BY_CHANNEL（0）或 GROUP_BY_GROUP_BY_SCAN_NUMBER（1）；writeArray 为 uint32 数组，允许 0～4294967295。端口位图保持物理端口位位置，例如选中 line4:7 置高使用 0xF0，不移位压缩。数组按虚拟通道分组，长度必须等于实际通道数×每通道采样数且最多 1000000 项；响应 sampsPerChanWritten 为实际写入数量",
        "CreateCICountEdgesChan" => "counter 为物理计数器字符串，可选 nameToAssignToChannel；edge 或 edgeRaw 必填：EDGE1_RISING（10280）/EDGE1_FALLING（10171）；initialCount 为 uint32；countDirection 或 countDirectionRaw 必填：COUNT_DIRECTION1_COUNT_UP（10128）、COUNT_DIRECTION1_COUNT_DOWN（10124）、COUNT_DIRECTION1_EXT_CONTROLLED（10326）。使用 Counter ReadCount 读取",
        "CreateCIFreqChan" => "counter 字符串，可选 nameToAssignToChannel；minVal/maxVal 有限且 0<minVal<maxVal；units 或 unitsRaw：FREQUENCY_UNITS3_HZ（10373）、FREQUENCY_UNITS3_TICKS（10304）或 FREQUENCY_UNITS3_FROM_CUSTOM_SCALE（10065，需 customScaleName）；edge/edgeRaw 为 EDGE1_RISING（10280）或 EDGE1_FALLING（10171）；measMethod/measMethodRaw 为 COUNTER_FREQUENCY_METHOD_LOW_FREQ_1_CTR（10105）、COUNTER_FREQUENCY_METHOD_HIGH_FREQ_2_CTR（10157）、COUNTER_FREQUENCY_METHOD_LARGE_RNG_2_CTR（10205）或 COUNTER_FREQUENCY_METHOD_DYN_AVG（16065）；measTime 有限正秒数、divisor 正整数。具体方法可占用多个硬件计数器，使用 Counter ReadMeasurement 读取",
        "CreateCOPulseChanFreq" => "counter 字符串，可选 nameToAssignToChannel；units/unitsRaw 为 FREQUENCY_UNITS2_HZ（10373）；idleState/idleStateRaw 为 LEVEL1_HIGH（10192）或 LEVEL1_LOW（10214）；initialDelay 为有限非负秒数；freq 为有限正 Hz 数，dutyCycle 严格介于 0 和 1。按需配合隐式定时和 Task Start；配置成功不表示已输出脉冲",
        "CreateCIAngEncoderChan" => EncoderDetails + "；units/unitsRaw 为 ANGLE_UNITS2_DEGREES（10146）、ANGLE_UNITS2_RADIANS（10273）、ANGLE_UNITS2_TICKS（10304）或 ANGLE_UNITS2_FROM_CUSTOM_SCALE（10065，需 customScaleName）；pulsesPerRev 为正整数，填每转脉冲数，不额外乘 X4；initialAngle 为有限值。使用 Counter ReadMeasurement 读取",
        "CreateCILinEncoderChan" => EncoderDetails + "；units/unitsRaw 为 LENGTH_UNITS3_METERS（10219）、LENGTH_UNITS3_INCHES（10379）、LENGTH_UNITS3_TICKS（10304）或 LENGTH_UNITS3_FROM_CUSTOM_SCALE（10065，需 customScaleName）；distPerPulse 为有限正距离，initialPos 为有限值。使用 Counter ReadMeasurement 读取",
        "CfgImplicitTiming" => "sampleMode/sampleModeRaw 为 ACQUISITION_TYPE_FINITE_SAMPS（10178）或 ACQUISITION_TYPE_CONT_SAMPS（10123）；sampsPerChan 为大于 0 的 uint64（protobuf JSON 字符串）。用于由脉冲参数或输入边沿决定时序的任务，不生成共享采样时钟",
        "CfgDigEdgeStartTrig" => "triggerSource 为非空硬件终端路径字符串；triggerEdge/triggerEdgeRaw 为 EDGE1_RISING（10280）或 EDGE1_FALLING（10171）。先配置后启动；Task Start 只把等待触发的任务准备好，外部或主任务触发才开始采样/输出",
        "ExportSignal" => "signalId/signalIdRaw 仅支持 SIGNAL_SAMPLE_CLOCK（12487）、SIGNAL_START_TRIGGER（12491）、SIGNAL_10_MHZ_REF_CLOCK（12536）、SIGNAL_20_MHZ_TIMEBASE_CLOCK（12486）、SIGNAL_COUNTER_OUTPUT_EVENT（12494）；outputTerminal 为非空输出终端路径。实际可路由性依硬件，可能占用 PFI/RTSI/PXI 资源",
        "SetTimingAttributeString" => "attribute/attributeRaw 仅支持 TIMING_ATTRIBUTE_REF_CLK_SRC（4886）、TIMING_ATTRIBUTE_SAMP_CLK_SRC（6226）、TIMING_ATTRIBUTE_SAMP_CLK_TIMEBASE_SRC（4872）、TIMING_ATTRIBUTE_SYNC_PULSE_SRC（8765）；value 为非空源终端字符串。按硬件方案配置共享时基/采样时钟/同步脉冲；禁止任意属性 ID",
        "SetTimingAttributeDouble" => "attribute/attributeRaw 仅支持 TIMING_ATTRIBUTE_REF_CLK_RATE（4885）、TIMING_ATTRIBUTE_SAMP_CLK_RATE（4932）、TIMING_ATTRIBUTE_SAMP_CLK_TIMEBASE_RATE（4867）；value 为有限正频率（Hz），必须匹配所选源；禁止任意属性 ID",
        "WriteCtrFreq" => "numSampsPerChan 为正整数，autoStart 为 bool（默认 false）；timeout 为有限非负秒数；dataLayout/dataLayoutRaw 为 GROUP_BY_GROUP_BY_CHANNEL（0）或 GROUP_BY_GROUP_BY_SCAN_NUMBER（1）；frequency 为有限正 Hz 数组，dutyCycle 为严格介于 0 和 1 的数组，两数组长度均须等于实际通道数×每通道采样数且最多 1000000 项；响应 numSampsPerChanWritten 为实际写入数量。需先创建脉冲频率输出通道，硬件决定运行中及缓冲更新能力",
        _ => throw new ArgumentOutOfRangeException(nameof(method))
    };
    private const string EncoderDetails = "counter 为物理计数器字符串，可选 nameToAssignToChannel；decodingType/decodingTypeRaw 为 ENCODER_TYPE2_X1（10090）、ENCODER_TYPE2_X2（10091）、ENCODER_TYPE2_X4（10092）或 ENCODER_TYPE2_TWO_PULSE_COUNTING（10313）；zidxEnable 为 bool，zidxVal 为有限值；zidxPhase/zidxPhaseRaw 必填：ENCODER_Z_INDEX_PHASE1_A_HIGH_B_HIGH（10040）、ENCODER_Z_INDEX_PHASE1_A_HIGH_B_LOW（10041）、ENCODER_Z_INDEX_PHASE1_A_LOW_B_HIGH（10042）、ENCODER_Z_INDEX_PHASE1_A_LOW_B_LOW（10043）";

    internal static void ValidateJson(string method, string json)
    {
        var descriptor = Daq.NidaqmxReflection.Descriptor.Services[0].Methods.Single(m => m.Name == method);
        IMessage request;
        try { request = JsonParser.Default.Parse(json, descriptor.InputType); }
        catch (Exception ex) when (ex is InvalidJsonException or InvalidProtocolBufferException or ArgumentException)
        { throw new LocalInputException($"{method} 请求不是有效的 protobuf JSON，未提交调用", ex); }
        Validate(method, request);
    }

    internal static void Validate(string method, IMessage request)
    {
        if (!Oneofs.TryGetValue(method, out var oneofs)) return;
        if (request.Descriptor.FindFieldByName("task").Accessor.GetValue(request) != null)
            throw new LocalInputException("请求不允许指定 task；任务由插件按会话别名注入");
        foreach (var name in oneofs)
            if (request.Descriptor.Oneofs.Single(o => o.Name == name).Accessor.GetCaseFieldDescriptor(request) == null)
                throw new LocalInputException($"{method} 缺少必填字段 {name}，未提交调用");
        switch (request)
        {
            case Daq.CreateDIChanRequest r:
                Text(r.Lines, "lines"); Enum(request, "line_grouping_enum", 0, 1); break;
            case Daq.CreateDOChanRequest r:
                Text(r.Lines, "lines"); Enum(request, "line_grouping_enum", 0, 1); break;
            case Daq.ReadDigitalU32Request r:
                Read(r.NumSampsPerChan, r.ArraySizeInSamps, r.Timeout); Enum(request, "fill_mode_enum", 0, 1); break;
            case Daq.ReadCounterU32ExRequest r:
                Read(r.NumSampsPerChan, r.ArraySizeInSamps, r.Timeout); Enum(request, "fill_mode_enum", 0, 1); break;
            case Daq.ReadCounterF64ExRequest r:
                Read(r.NumSampsPerChan, r.ArraySizeInSamps, r.Timeout); Enum(request, "fill_mode_enum", 0, 1); break;
            case Daq.WriteDigitalU32Request r:
                Write(r.NumSampsPerChan, r.WriteArray.Count, r.Timeout); Enum(request, "data_layout_enum", 0, 1); break;
            case Daq.WriteCtrFreqRequest r:
                Write(r.NumSampsPerChan, r.Frequency.Count, r.Timeout); Enum(request, "data_layout_enum", 0, 1);
                if (r.Frequency.Count != r.DutyCycle.Count) Fail("frequency 与 dutyCycle 的数组长度必须相同");
                foreach (double frequency in r.Frequency) Positive(frequency, "frequency");
                foreach (double duty in r.DutyCycle) Duty(duty);
                break;
            case Daq.CreateCICountEdgesChanRequest r:
                Text(r.Counter, "counter"); Enum(request, "edge_enum", 10280, 10171);
                Enum(request, "count_direction_enum", 10128, 10124, 10326); break;
            case Daq.CreateCIFreqChanRequest r:
                Text(r.Counter, "counter"); Positive(r.MinVal, "minVal"); Positive(r.MaxVal, "maxVal");
                if (r.MaxVal <= r.MinVal) Fail("maxVal 必须大于 minVal");
                CustomUnits(request, r.CustomScaleName, 10373, 10304, 10065);
                Enum(request, "edge_enum", 10280, 10171); Enum(request, "meas_method_enum", 10105, 10157, 10205, 16065);
                Positive(r.MeasTime, "measTime"); if (r.Divisor == 0) Fail("divisor 必须大于 0"); break;
            case Daq.CreateCOPulseChanFreqRequest r:
                Text(r.Counter, "counter"); Enum(request, "units_enum", 10373); Enum(request, "idle_state_enum", 10192, 10214);
                Nonnegative(r.InitialDelay, "initialDelay"); Positive(r.Freq, "freq"); Duty(r.DutyCycle); break;
            case Daq.CreateCIAngEncoderChanRequest r:
                Encoder(request, r.Counter, r.ZidxVal); CustomUnits(request, r.CustomScaleName, 10146, 10273, 10304, 10065);
                if (r.PulsesPerRev == 0) Fail("pulsesPerRev 必须大于 0；填写编码器每转脉冲数，不重复乘解码倍频");
                Finite(r.InitialAngle, "initialAngle"); break;
            case Daq.CreateCILinEncoderChanRequest r:
                Encoder(request, r.Counter, r.ZidxVal); CustomUnits(request, r.CustomScaleName, 10219, 10379, 10304, 10065);
                Positive(r.DistPerPulse, "distPerPulse"); Finite(r.InitialPos, "initialPos"); break;
            case Daq.CfgImplicitTimingRequest r:
                Enum(request, "sample_mode_enum", 10178, 10123);
                if (r.SampsPerChan == 0) Fail("sampsPerChan 必须大于 0"); break;
            case Daq.CfgDigEdgeStartTrigRequest r:
                Text(r.TriggerSource, "triggerSource"); Enum(request, "trigger_edge_enum", 10280, 10171); break;
            case Daq.ExportSignalRequest r:
                Enum(request, "signal_id_enum", 12487, 12491, 12536, 12486, 12494); Text(r.OutputTerminal, "outputTerminal"); break;
            case Daq.SetTimingAttributeStringRequest r:
                Enum(request, "attribute_enum", 4886, 6226, 4872, 8765); Text(r.Value, "value"); break;
            case Daq.SetTimingAttributeDoubleRequest r:
                Enum(request, "attribute_enum", 4885, 4932, 4867); Positive(r.Value, "value"); break;
        }
    }

    internal static (int SamplesPerChannel, int ArrayLength)? WriteShape(IMessage request) => request switch
    {
        Daq.WriteDigitalU32Request r => (r.NumSampsPerChan, r.WriteArray.Count),
        Daq.WriteCtrFreqRequest r => (r.NumSampsPerChan, r.Frequency.Count),
        _ => null
    };

    private static int Enum(IMessage request, string oneofName, params int[] allowed)
    {
        var field = request.Descriptor.Oneofs.Single(o => o.Name == oneofName).Accessor.GetCaseFieldDescriptor(request)!;
        int value = Convert.ToInt32(field.Accessor.GetValue(request));
        if (!allowed.Contains(value)) Fail($"{field.JsonName} 不在本步骤支持的取值范围（{string.Join(",", allowed)}）");
        return value;
    }
    private static void CustomUnits(IMessage request, string scale, params int[] allowed)
    { if (Enum(request, "units_enum", allowed) == 10065) Text(scale, "customScaleName"); }
    private static void Encoder(IMessage request, string counter, double index)
    { Text(counter, "counter"); Enum(request, "decoding_type_enum", 10090, 10091, 10092, 10313); Enum(request, "zidx_phase_enum", 10040, 10041, 10042, 10043); Finite(index, "zidxVal"); }
    private static void Read(int samples, uint capacity, double timeout)
    {
        if (samples is < -1 or 0 || capacity is < 1 or > MaximumSamples || samples > capacity)
            Fail("读取采样数须为 -1 或正数；数组容量须为 1～1000000 且不小于指定的每通道采样数");
        Nonnegative(timeout, "timeout");
    }
    private static void Write(int samples, int count, double timeout)
    {
        if (samples <= 0 || count is < 1 or > MaximumSamples || samples > count)
            Fail("写入采样数必须为正，数组须为 1～1000000 项且不少于每通道采样数");
        Nonnegative(timeout, "timeout");
    }
    private static void Text(string value, string field)
    { if (string.IsNullOrWhiteSpace(value) || value.Contains('\0') || value.Length > 4096) Fail($"{field} 必须为非空字符串，不能含 NUL，最长 4096 字符"); }
    private static void Finite(double value, string field)
    { if (!double.IsFinite(value)) Fail($"{field} 必须为有限数值"); }
    private static void Positive(double value, string field)
    { Finite(value, field); if (value <= 0) Fail($"{field} 必须大于 0"); }
    private static void Nonnegative(double value, string field)
    { Finite(value, field); if (value < 0) Fail($"{field} 必须大于等于 0；本步骤不接受无限等待"); }
    private static void Duty(double value)
    { Finite(value, "dutyCycle"); if (value <= 0 || value >= 1) Fail("dutyCycle 必须严格介于 0 和 1 之间"); }
    private static void Fail(string message) => throw new LocalInputException($"DAQmx 参数无效：{message}；未提交目标调用");
}
