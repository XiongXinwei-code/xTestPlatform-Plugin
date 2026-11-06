namespace ZlgEtherCat.Native;

public sealed class ZlgNativeException : Exception
{
    public string Function { get; }
    public int ErrorCode { get; }
    public ZlgNativeException(string function, int errorCode, string detail)
        : base($"ZLG 接口 {function} 调用失败，错误码 {errorCode}（0x{errorCode:X8}）：{detail}")
    { Function = function; ErrorCode = errorCode; }
}
