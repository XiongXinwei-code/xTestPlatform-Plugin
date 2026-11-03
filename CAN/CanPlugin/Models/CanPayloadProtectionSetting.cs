using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using MessagePack;
using xTestPlatform.Core.Models.StepSettings;

namespace CAN.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CanPayloadByteOrder { LittleEndian = 0, BigEndian = 1 }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CanCounterOverflow { Wrap = 0, Hold = 1, Error = 2 }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CanCrcFieldTreatment { Skip = 0, Zero = 1 }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CanCrcDataIdPlacement { BeforeData = 0, AfterData = 1 }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CanCrcDataIdMode
{
    Fixed = 0, Profile2 = 1, Profile1Both = 2, Profile1Alt = 3,
    Profile1Low = 4, Profile1Nibble = 5
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CanCrcAlgorithm
{
    Crc8 = 0, Crc8SaeJ1850 = 1, Crc8Autosar = 2, Crc16Modbus = 3,
    Crc16CcittFalse = 4, Crc32IsoHdlc = 5, Custom = 6, Crc8MaximDow = 7,
    Crc8Rohc = 8, Crc16Arc = 9, Crc16X25 = 10, Crc16Xmodem = 11,
    Crc16Kermit = 12, Crc16Usb = 13, Crc16Dnp = 14, Crc32C = 15,
    Crc32Mpeg2 = 16, Crc32Bzip2 = 17, Crc32Jamcrc = 18,
    Crc64Ecma182 = 19, Crc64Xz = 20
}

/// <summary>应用层载荷处理；字节偏移从零开始且包含结束位置，不是 CAN 控制器的链路 CRC。</summary>
[MessagePackObject(true)]
[System.Text.Json.Serialization.JsonConverter(typeof(PluginCommon.Serialization.StrictSettingJsonConverter))]
public sealed class CanPayloadProtectionSetting : INotifyPropertyChanged
{
    private bool _rcEnabled;
    private int _rcStartByte;
    private int _rcEndByte;
    private CanPayloadByteOrder _rcByteOrder;
    private uint _rcInitialValue;
    private uint _rcMinimum;
    private uint _rcMaximum = 255;
    private long _rcStep = 1;
    private CanCounterOverflow _rcOverflow;
    private bool _crcEnabled;
    private int _crcStartByte = 7;
    private int _crcEndByte = 7;
    private CanPayloadByteOrder _crcByteOrder;
    private CanCrcAlgorithm _crcAlgorithm;
    private int _crcDataStartByte;
    private int _crcDataEndByte = -1;
    private CanCrcFieldTreatment _crcFieldTreatment;
    private int _crcWidth = 8;
    private string _crcName = "Custom";
    private string _crcPolynomial = "0x07";
    private string _crcInitialValue = "0x00";
    private string _crcXorOutput = "0x00";
    private bool _crcReflectInput;
    private bool _crcReflectOutput;
    private bool _crcDataIdEnabled;
    private string _crcDataIdHex = "";
    private CanCrcDataIdPlacement _crcDataIdPlacement;
    private bool _rcUseBitField;
    private int _rcBitOffset;
    private int _rcBitLength = 4;
    private CanCrcDataIdMode _crcDataIdMode;
    private bool _crcDataIdUseProfilePlacement = true;
    private string[] _crcDataIdTable = Enumerable.Repeat("", 16).ToArray();
    private bool _crcDataIdTableUseExpression;
    private string _crcDataIdTableExpression = "";
    private string _crcProfile1DataIdHex = "0000";
    private int _crcCounterByte = 1;
    private int _crcCounterBitOffset;
    private int _crcDataIdNibbleByte = 1;
    private int _crcDataIdNibbleBitOffset = 4;

    public bool RcEnabled { get => _rcEnabled; set => Set(ref _rcEnabled, value); }
    public int RcStartByte { get => _rcStartByte; set => Set(ref _rcStartByte, value); }
    public int RcEndByte { get => _rcEndByte; set => Set(ref _rcEndByte, value); }
    public CanPayloadByteOrder RcByteOrder { get => _rcByteOrder; set => Set(ref _rcByteOrder, value); }
    public uint RcInitialValue { get => _rcInitialValue; set => Set(ref _rcInitialValue, value); }
    public uint RcMinimum { get => _rcMinimum; set => Set(ref _rcMinimum, value); }
    public uint RcMaximum { get => _rcMaximum; set => Set(ref _rcMaximum, value); }
    public long RcStep { get => _rcStep; set => Set(ref _rcStep, value); }
    public CanCounterOverflow RcOverflow { get => _rcOverflow; set => Set(ref _rcOverflow, value); }
    public bool CrcEnabled { get => _crcEnabled; set => Set(ref _crcEnabled, value); }
    public int CrcStartByte { get => _crcStartByte; set => Set(ref _crcStartByte, value); }
    public int CrcEndByte { get => _crcEndByte; set => Set(ref _crcEndByte, value); }
    public CanPayloadByteOrder CrcByteOrder { get => _crcByteOrder; set => Set(ref _crcByteOrder, value); }
    public CanCrcAlgorithm CrcAlgorithm { get => _crcAlgorithm; set => Set(ref _crcAlgorithm, value); }
    public int CrcDataStartByte { get => _crcDataStartByte; set => Set(ref _crcDataStartByte, value); }
    public int CrcDataEndByte { get => _crcDataEndByte; set => Set(ref _crcDataEndByte, value); }
    public CanCrcFieldTreatment CrcFieldTreatment { get => _crcFieldTreatment; set => Set(ref _crcFieldTreatment, value); }
    public int CrcWidth { get => _crcWidth; set => Set(ref _crcWidth, value); }
    public string CrcName { get => _crcName; set => Set(ref _crcName, value); }
    public string CrcPolynomial { get => _crcPolynomial; set => Set(ref _crcPolynomial, value); }
    public string CrcInitialValue { get => _crcInitialValue; set => Set(ref _crcInitialValue, value); }
    public string CrcXorOutput { get => _crcXorOutput; set => Set(ref _crcXorOutput, value); }
    public bool CrcReflectInput { get => _crcReflectInput; set => Set(ref _crcReflectInput, value); }
    public bool CrcReflectOutput { get => _crcReflectOutput; set => Set(ref _crcReflectOutput, value); }
    public bool CrcDataIdEnabled { get => _crcDataIdEnabled; set => Set(ref _crcDataIdEnabled, value); }
    public string CrcDataIdHex { get => _crcDataIdHex; set => Set(ref _crcDataIdHex, value); }
    public CanCrcDataIdPlacement CrcDataIdPlacement { get => _crcDataIdPlacement; set => Set(ref _crcDataIdPlacement, value); }
    public bool RcUseBitField { get => _rcUseBitField; set => Set(ref _rcUseBitField, value); }
    public int RcBitOffset { get => _rcBitOffset; set => Set(ref _rcBitOffset, value); }
    public int RcBitLength { get => _rcBitLength; set => Set(ref _rcBitLength, value); }
    public CanCrcDataIdMode CrcDataIdMode { get => _crcDataIdMode; set => Set(ref _crcDataIdMode, value); }
    public bool CrcDataIdUseProfilePlacement { get => _crcDataIdUseProfilePlacement; set => Set(ref _crcDataIdUseProfilePlacement, value); }
    public string[] CrcDataIdTable { get => _crcDataIdTable; set => Set(ref _crcDataIdTable, value); }
    public bool CrcDataIdTableUseExpression { get => _crcDataIdTableUseExpression; set => Set(ref _crcDataIdTableUseExpression, value); }
    [ExpressionField]
    public string CrcDataIdTableExpression { get => _crcDataIdTableExpression; set => Set(ref _crcDataIdTableExpression, value); }
    public string CrcProfile1DataIdHex { get => _crcProfile1DataIdHex; set => Set(ref _crcProfile1DataIdHex, value); }
    public int CrcCounterByte { get => _crcCounterByte; set => Set(ref _crcCounterByte, value); }
    public int CrcCounterBitOffset { get => _crcCounterBitOffset; set => Set(ref _crcCounterBitOffset, value); }
    public int CrcDataIdNibbleByte { get => _crcDataIdNibbleByte; set => Set(ref _crcDataIdNibbleByte, value); }
    public int CrcDataIdNibbleBitOffset { get => _crcDataIdNibbleBitOffset; set => Set(ref _crcDataIdNibbleBitOffset, value); }

    /// <summary>复制配置值，不复制编辑器事件订阅或运行时计数。</summary>
    public CanPayloadProtectionSetting Copy() => new()
    {
        RcEnabled = RcEnabled, RcStartByte = RcStartByte, RcEndByte = RcEndByte,
        RcByteOrder = RcByteOrder, RcInitialValue = RcInitialValue, RcMinimum = RcMinimum,
        RcMaximum = RcMaximum, RcStep = RcStep, RcOverflow = RcOverflow,
        CrcEnabled = CrcEnabled, CrcStartByte = CrcStartByte, CrcEndByte = CrcEndByte,
        CrcByteOrder = CrcByteOrder, CrcAlgorithm = CrcAlgorithm,
        CrcDataStartByte = CrcDataStartByte, CrcDataEndByte = CrcDataEndByte,
        CrcFieldTreatment = CrcFieldTreatment, CrcWidth = CrcWidth, CrcName = CrcName,
        CrcPolynomial = CrcPolynomial, CrcInitialValue = CrcInitialValue,
        CrcXorOutput = CrcXorOutput, CrcReflectInput = CrcReflectInput, CrcReflectOutput = CrcReflectOutput,
        CrcDataIdEnabled = CrcDataIdEnabled, CrcDataIdHex = CrcDataIdHex, CrcDataIdPlacement = CrcDataIdPlacement,
        RcUseBitField = RcUseBitField, RcBitOffset = RcBitOffset, RcBitLength = RcBitLength,
        CrcDataIdMode = CrcDataIdMode, CrcDataIdUseProfilePlacement = CrcDataIdUseProfilePlacement,
        CrcDataIdTable = CrcDataIdTable?.ToArray()!,
        CrcDataIdTableUseExpression = CrcDataIdTableUseExpression, CrcDataIdTableExpression = CrcDataIdTableExpression,
        CrcProfile1DataIdHex = CrcProfile1DataIdHex, CrcCounterByte = CrcCounterByte,
        CrcCounterBitOffset = CrcCounterBitOffset, CrcDataIdNibbleByte = CrcDataIdNibbleByte,
        CrcDataIdNibbleBitOffset = CrcDataIdNibbleBitOffset
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    public override string ToString() => CAN.Helpers.CanSettingText.Format(this);
    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
