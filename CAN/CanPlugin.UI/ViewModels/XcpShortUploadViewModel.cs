using CAN.XCP;
using CAN.XCP.Models;

namespace CAN.UI.ViewModels;

public class XcpShortUploadViewModel : XcpViewModelBase<XcpShortUploadSetting>
{
    private static readonly XcpAddressExtension[] AddrExts =
        [XcpAddressExtension.None, XcpAddressExtension.Odt, XcpAddressExtension.Daq];
    private static readonly XcpByteOrder[] ByteOrders =
        [XcpByteOrder.LittleEndian, XcpByteOrder.BigEndian];

    public string Address
    {
        get => Setting?.Address ?? string.Empty;
        set { if (Setting == null || Setting.Address == value) return; Setting.Address = value; OnPropertyChanged(); QueueSave(); }
    }

    public int AddressExtensionIndex
    {
        get => Setting == null ? 0 : Array.IndexOf(AddrExts, Setting.AddressExtension);
        set { if (Setting == null || value < 0 || value >= AddrExts.Length) return; Setting.AddressExtension = AddrExts[value]; OnPropertyChanged(); QueueSave(); }
    }

    public int ReadLength
    {
        get => Setting?.ReadLength ?? 4;
        set { if (Setting == null || Setting.ReadLength == value) return; Setting.ReadLength = value; OnPropertyChanged(); QueueSave(); }
    }

    public int ByteOrderIndex
    {
        get => Setting == null ? 0 : Array.IndexOf(ByteOrders, Setting.ByteOrder);
        set { if (Setting == null || value < 0 || value >= ByteOrders.Length) return; Setting.ByteOrder = ByteOrders[value]; OnPropertyChanged(); QueueSave(); }
    }

    public string ResultVariable
    {
        get => Setting?.ResultVariable ?? string.Empty;
        set { if (Setting == null || Setting.ResultVariable == value) return; Setting.ResultVariable = value; OnPropertyChanged(); QueueSave(); }
    }
}
