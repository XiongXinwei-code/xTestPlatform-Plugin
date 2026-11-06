using System.Text.Json.Serialization;

namespace ZlgEtherCat.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MasterState { Init = 1, PreOperational = 2, Bootstrap = 3, SafeOperational = 4, Operational = 8 }
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PiReinitMode { Auto = 0, ClearAll = 1, PreserveAsynchronous = 2, PreserveSynchronous = 3, PreserveAll = 4 }
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ExchangeMode { Queue, Direct, Cycle, Manual }
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AcquisitionPriority { Normal = 0, AboveNormal = 1, Highest = 2 }
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PdoDataType { Bytes, Boolean, UInt8, Int8, UInt16, Int16, UInt32, Int32, UInt64, Int64, Float32, Float64 }
