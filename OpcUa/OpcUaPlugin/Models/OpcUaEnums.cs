namespace OpcUa.Models;

/// <summary>OPC UA 安全策略</summary>
public enum OpcUaSecurityPolicy
{
    None = 0,
    Basic256Sha256 = 1,
    Aes128Sha256RsaOaep = 2,
    Aes256Sha256RsaPss = 3
}

/// <summary>认证模式</summary>
public enum OpcUaAuthMode
{
    Anonymous = 0,
    UserPassword = 1
}

/// <summary>OPC UA 数据类型（写入时指定）</summary>
public enum OpcUaDataType
{
    Auto = 0,
    Boolean = 1,
    Int16 = 2,
    UInt16 = 3,
    Int32 = 4,
    UInt32 = 5,
    Int64 = 6,
    UInt64 = 7,
    Float = 8,
    Double = 9,
    String = 10
}

/// <summary>订阅比较模式</summary>
public enum OpcUaCompareMode
{
    Equal = 0,
    NotEqual = 1,
    GreaterThan = 2,
    LessThan = 3,
    Contains = 4
}
