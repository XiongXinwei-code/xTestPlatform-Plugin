using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace NiDaq.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AoStopPolicy { Unspecified, DeviceDefined, WriteFinalValues }

[MessagePackObject(true)]
public sealed class NiDaqAoChannel : INotifyPropertyChanged
{
    private string _physicalChannel = "", _columnName = "";
    private double _minValue = -10, _maxValue = 10;
    public string PhysicalChannel { get => _physicalChannel; set => Set(ref _physicalChannel, value); }
    public string ColumnName { get => _columnName; set => Set(ref _columnName, value); }
    public double MinValue { get => _minValue; set => Set(ref _minValue, value); }
    public double MaxValue { get => _maxValue; set => Set(ref _maxValue, value); }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

[MessagePackObject(true)]
public sealed class NiDaqAoConfigSetting
{
    [ExpressionField] public string TaskName { get; set; } = "\"AoTask1\"";
    public ObservableCollection<NiDaqAoChannel> Channels { get; set; } = [];
    [ExpressionField] public string InitialValues { get; set; } = "";
    public AoStopPolicy StopPolicy { get; set; } = AoStopPolicy.Unspecified;
    [ExpressionField] public string FinalValues { get; set; } = "";
    public int CleanupTimeoutMs { get; set; } = 5000;
    public int TimeoutMs { get; set; } = 10000;
}
