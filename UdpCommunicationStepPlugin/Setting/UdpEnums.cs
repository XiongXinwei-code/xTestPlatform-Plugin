namespace UdpCommunicationStepPlugin.Setting;

public enum UdpOperationMode
{
    SendOnly,
    SendAndWaitForResponse
}

public enum UdpDataFormat
{
    Utf8Text,
    Hex
}

public enum UdpResponseMatchMode
{
    AnyResponse,
    Exact,
    Contains
}
