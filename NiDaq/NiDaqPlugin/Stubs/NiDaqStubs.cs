#if NIDAQ_STUB
// 桩定义：在未安装 NI-DAQmx 驱动的开发机上提供类型定义以保证编译通过。
// 安装 NI-DAQmx 后，NI NuGet 包会提供真实实现，此文件自动被排除（NIDAQ_STUB 不再定义）。

using System;

namespace NationalInstruments.DAQmx
{
    public class Task : IDisposable
    {
        public AIChannelCollection AIChannels => new();
        public DIChannelCollection DIChannels => new();
        public DOChannelCollection DOChannels => new();
        public CIChannelCollection CIChannels => new();
        public TimingConfiguration Timing => new();
        public TriggerConfiguration Triggers => new();
        public TaskStream Stream => new();
        public string[] Devices => new[] { "Dev1" };
        public void Start() { }
        public void Stop() { }
        public void Dispose() { }
    }

    public class TaskStream { }

    public class AIChannelCollection
    {
        public void CreateVoltageChannel(string channel, string name, AITerminalConfiguration terminal, double min, double max, AIVoltageUnits units) { }
    }

    public class DIChannelCollection
    {
        public void CreateChannel(string channel, string name, ChannelLineGrouping grouping) { }
    }

    public class DOChannelCollection
    {
        public void CreateChannel(string channel, string name, ChannelLineGrouping grouping) { }
    }

    public class CIChannelCollection
    {
        public void CreateAngularEncoderChannel(string channel, string name, CIEncoderDecodingType decoding, bool zIndex, double zPhase, CIEncoderZIndexPhase zIndexPhase, int ppr, double initialAngle, CIAngularEncoderUnits units) { }
    }

    public class TimingConfiguration
    {
        public void ConfigureSampleClock(string source, double rate, SampleClockActiveEdge edge, SampleQuantityMode mode, int samplesPerChannel = 0) { }
    }

    public class TriggerConfiguration
    {
        public StartTrigger StartTrigger => new();
    }

    public class StartTrigger
    {
        public void ConfigureDigitalEdgeTrigger(string source, DigitalEdgeStartTriggerEdge edge) { }
    }

    public class AnalogMultiChannelReader
    {
        public AnalogMultiChannelReader(TaskStream stream) { }
        public double[,] ReadMultiSample(int samples) => new double[1, samples];
    }

    public class DigitalSingleChannelReader
    {
        public DigitalSingleChannelReader(TaskStream stream) { }
        public uint ReadSingleSamplePortUInt32() => 0;
    }

    public class DigitalSingleChannelWriter
    {
        public DigitalSingleChannelWriter(TaskStream stream) { }
        public void WriteSingleSampleSingleLine(bool autoStart, bool value) { }
        public void WriteSingleSamplePort(bool autoStart, uint value) { }
    }

    public class CounterSingleChannelReader
    {
        public CounterSingleChannelReader(TaskStream stream) { }
        public double ReadSingleSampleDouble() => 0;
    }

    public class CounterMultiChannelReader
    {
        public CounterMultiChannelReader(TaskStream stream) { }
        public double[,] ReadMultiSampleDouble(int samples) => new double[1, samples];
    }

    public enum AITerminalConfiguration { Differential, Rse, Nrse, Pseudodifferential }
    public enum AIVoltageUnits { Volts }
    public enum ChannelLineGrouping { OneChannelForAllLines }
    public enum SampleClockActiveEdge { Rising }
    public enum SampleQuantityMode { FiniteSamples, ContinuousSamples }
    public enum CIEncoderDecodingType { X1, X2, X4 }
    public enum CIAngularEncoderUnits { Ticks }
    public enum CIEncoderZIndexPhase { AHighBHigh, AHighBLow, ALowBHigh, ALowBLow }
    public enum DigitalEdgeStartTriggerEdge { Rising, Falling }

    public class DaqException : Exception
    {
        public DaqException() { }
        public DaqException(string message) : base(message) { }
    }
}

namespace NationalInstruments.Tdms
{
    public enum TdmsFileAccess { Read, Create }
    public enum TdmsDataType { Double, Single, Int32, Int64, String }

    public class TdmsFile : IDisposable
    {
        public TdmsFile(string path) { }
        public TdmsFile(string path, TdmsFileAccess access) { }
        public TdmsChannelGroup AddChannelGroup(string name) => new();
        public TdmsChannelGroupCollection GetChannelGroups() => new();
        public void Open() { }
        public void Save() { }
        public void Dispose() { }
    }

    public class TdmsChannelGroupCollection : List<TdmsChannelGroup> { }

    public class TdmsChannelGroup
    {
        public TdmsChannel AddChannel(string name, TdmsDataType dataType) => new(name);
        public TdmsChannelCollection GetChannels() => new();
    }

    public class TdmsChannelCollection : List<TdmsChannel> { }

    public class TdmsChannel
    {
        public string Name { get; }
        public long DataCount => 0;
        public TdmsChannel(string name = "") { Name = name; }
        public void AppendData<T>(T[] data) { }
        public T[] GetData<T>(long offset, int count) => new T[count];
    }
}
#endif
