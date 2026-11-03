namespace CAN.Adapters.Zlg;

/// <summary>Device names from the bundled zlgcan.h; availability still depends on its kernel DLL and driver.</summary>
public sealed record ZlgDeviceInfo(uint Type, string Name, string KernelLibrary)
{
    public string Display => $"{Name} ({Type})";
    public string ConfigurationHint => ZlgDeviceCatalog.Describe(Type);
}

public static class ZlgDeviceCatalog
{
    public static IReadOnlyList<ZlgDeviceInfo> Devices { get; } = Array.AsReadOnly(new ZlgDeviceInfo[]
    {
        new(1, "PCI5121", "PCI51XXE.dll"),
        new(2, "PCI9810", "PCI9810.dll"),
        new(3, "USBCAN1", "USBCAN.dll"),
        new(4, "USBCAN2", "USBCAN.dll"),
        new(5, "PCI9820", "PCI9820.dll"),
        new(6, "CAN232", ""),
        new(7, "PCI5110", "PCI51XXE.dll"),
        new(8, "CANLITE", ""),
        new(9, "ISA9620", ""),
        new(10, "ISA5420", ""),
        new(11, "PC104CAN", ""),
        new(12, "CANETUDP", "CANETE.dll"),
        new(12, "CANETE", "CANETE.dll"),
        new(13, "DNP9810", ""),
        new(14, "PCI9840", "PCI9840B.dll"),
        new(15, "PC104CAN2", ""),
        new(16, "PCI9820I", "PCI9820I.dll"),
        new(17, "CANETTCP", "CANET_TCP.dll"),
        new(18, "PCIE_9220", ""),
        new(19, "PCI5010U", "pci50xx_u.dll"),
        new(20, "USBCAN_E_U", "USBCAN_E_64.dll"),
        new(21, "USBCAN_2E_U", "USBCAN_E_64.dll"),
        new(22, "PCI5020U", "pci50xx_u.dll"),
        new(23, "EG20T_CAN", "topcliff_can.dll"),
        new(24, "PCIE9221", "pcie9221.dll"),
        new(25, "WIFICAN_TCP", "CANWIFI_TCP.dll"),
        new(26, "WIFICAN_UDP", "CANWIFI_UDP.dll"),
        new(27, "PCIe9120", "pcie9120.dll"),
        new(28, "PCIe9110", "pcie9110.dll"),
        new(29, "PCIe9140", "pcie9140.dll"),
        new(31, "USBCAN_4E_U", "USBCAN_4E_U_X64.dll"),
        new(32, "CANDTU_200UR", "CANDTU_x64.DLL"),
        new(33, "CANDTU_MINI", "CANDTU_MINI.dll"),
        new(34, "USBCAN_8E_U", "USBCAN_8E_U_x64.dll"),
        new(35, "CANREPLAY", "CAN_REPLAY.dll"),
        new(36, "CANDTU_NET", "CANDTU_NET.dll"),
        new(37, "CANDTU_100UR", "CANDTU_x64.dll"),
        new(38, "PCIE_CANFD_100U", "zpcfd.dll"),
        new(39, "PCIE_CANFD_200U", "zpcfd.dll"),
        new(40, "PCIE_CANFD_400U", "zpcfd.dll"),
        new(41, "USBCANFD_200U", "USBCANFD.dll"),
        new(42, "USBCANFD_100U", "USBCANFD.dll"),
        new(43, "USBCANFD_MINI", "USBCANFD.dll"),
        new(44, "CANFDCOM_100IE", "CANFDCOM.dll"),
        new(45, "CANSCOPE", "CANDevice.dll"),
        new(46, "CLOUD", "ZlgCloud.dll"),
        new(47, "CANDTU_NET_400", "CANDTU_NET.dll"),
        new(48, "CANFDNET_TCP", "CANFDNET.dll"),
        new(48, "CANFDNET_200U_TCP", "CANFDNET.dll"),
        new(49, "CANFDNET_UDP", "CANFDNET.dll"),
        new(49, "CANFDNET_200U_UDP", "CANFDNET.dll"),
        new(50, "CANFDWIFI_TCP", "CANFDNET.dll"),
        new(50, "CANFDWIFI_100U_TCP", "CANFDNET.dll"),
        new(51, "CANFDWIFI_UDP", "CANFDNET.dll"),
        new(51, "CANFDWIFI_100U_UDP", "CANFDNET.dll"),
        new(52, "CANFDNET_400U_TCP", "CANFDNET.dll"),
        new(53, "CANFDNET_400U_UDP", "CANFDNET.dll"),
        new(54, "CANFDBLUE_200U", "CANFDBlue.dll"),
        new(55, "CANFDNET_100U_TCP", "CANFDNET.dll"),
        new(56, "CANFDNET_100U_UDP", "CANFDNET.dll"),
        new(57, "CANFDNET_800U_TCP", "CANFDNET.dll"),
        new(58, "CANFDNET_800U_UDP", "CANFDNET.dll"),
        new(59, "USBCANFD_800U", "USBCANFD800U.dll"),
        new(60, "PCIE_CANFD_100U_EX", "xpcfd.dll"),
        new(61, "PCIE_CANFD_400U_EX", "xpcfd.dll"),
        new(62, "PCIE_CANFD_200U_MINI", "xpcfd.dll"),
        new(63, "PCIE_CANFD_200U_EX", "xpcfd.dll"),
        new(63, "PCIE_CANFD_200U_M2", "xpcfd.dll"),
        new(64, "CANFDDTU_400_TCP", "CANFDNET.dll"),
        new(65, "CANFDDTU_400_UDP", "CANFDNET.dll"),
        new(66, "CANFDWIFI_200U_TCP", "CANFDNET.dll"),
        new(67, "CANFDWIFI_200U_UDP", "CANFDNET.dll"),
        new(68, "CANFDDTU_800ER_TCP", "CANFDNET.dll"),
        new(69, "CANFDDTU_800ER_UDP", "CANFDNET.dll"),
        new(70, "CANFDDTU_800EWGR_TCP", "CANFDNET.dll"),
        new(71, "CANFDDTU_800EWGR_UDP", "CANFDNET.dll"),
        new(72, "CANFDDTU_600EWGR_TCP", "CANFDNET.dll"),
        new(73, "CANFDDTU_600EWGR_UDP", "CANFDNET.dll"),
        new(74, "CANFDDTU_CASCADE_TCP", "CANFDNET.dll"),
        new(75, "CANFDDTU_CASCADE_UDP", "CANFDNET.dll"),
        new(76, "USBCANFD_400U", "USBCANFD.dll"),
        new(77, "CANFDDTU_200U", "USBCANFD.dll"),
        new(78, "ZPSCANFD_TCP", "ZPSCANFD.dll"),
        new(79, "ZPSCANFD_USB", "ZPSCANFD.dll"),
        new(80, "CANFDBRIDGE_PLUS", "USBCANFD.dll"),
        new(81, "CANFDDTU_300U", "USBCANFD.dll"),
        new(82, "PCIE_CANFD_800U", "xpcfd.dll"),
        new(83, "PCIE_CANFD_1200U", "xpcfd.dll"),
        new(84, "MINI_PCIE_CANFD", "xpcfd.dll"),
        new(85, "USBCANFD_800H", "USBCANFD.dll"),
        new(86, "BG002", "USBCANFD.dll"),
        new(87, "BG004", "USBCANFD.dll"),
        new(98, "OFFLINE_DEVICE", ""),
        new(99, "VIRTUAL_DEVICE", "VirtualUSBCAN.dll"),
        new(30, "PCI5010P", "pci5010p.dll") // dll_cfg.ini additionally declares this legacy type.
    });
    public static uint Parse(string name)
    {
        string normalized = Normalize(name);
        if (normalized is "USBCANI") return 3;
        if (normalized is "USBCANII") return 4;
        var model = Devices.FirstOrDefault(item => Normalize(item.Name) == normalized);
        return model?.Type ?? throw new ArgumentException($"未知 ZLG 设备类型 '{name}'；请使用连接页 SDK 型号列表中的名称。");
    }
    private static string Normalize(string name)
    {
        string text = name.Trim().ToUpperInvariant();
        if (text.StartsWith("ZCAN_", StringComparison.Ordinal)) text = text[5..];
        return text.Replace("_", "").Replace("-", "");
    }
    public static bool IsNetwork(uint type) => type is 12 or 17 or 25 or 26 or 36 or 47 or 78
        || type is >= 48 and <= 53 or >= 55 and <= 58 or >= 64 and <= 75;
    public static bool IsUdp(uint type) => type is 12 or 26 or 49 or 51 or 53 or 56 or 58 or 65 or 67 or 69 or 71 or 73 or 75;
    public static bool IsCanFd(uint type) => type is >= 38 and <= 44 or 46 or >= 48 and <= 87 or 99;
    public static bool IsAdditionalUsbFd(uint type) => type is 59 or 76 or 77 or 80 or 81 or 85 or 86 or 87;
    public static bool IsPostConfigured(uint type) => type is 32 or 37 or 78 or 79;
    public static bool UsesExternalBusConfiguration(uint type) => IsNetwork(type) && type != 78 || type is 44 or 46 or 54 or 99;
    public static bool RequiresFilePath(uint type) => type == 44;
    public static bool HasProtocolProperty(uint type) => IsNetwork(type) && IsCanFd(type) || type is 54 or 59 or 76 or 77 or 79 or 80 or 81 or 85 or 86 or 87 or 99;
    public static string Describe(uint type)
    {
        string family = type == 46 ? "Cloud：填写账号与密码表达式；仅共享本插件创建的同身份云登录，最后连接关闭后登出。"
            : type == 78 ? "ZPSCANFD TCP：网络参数在 InitCAN 前设置，波特率和终端电阻在 InitCAN 后设置。"
            : IsNetwork(type) ? "网络设备：填写 SDK work_mode、IP、工作端口和本地端口。网络连接参数在 InitCAN 前设置。"
            : type == 44 ? "CANFDCOM：填写厂商配置文件路径，映射 SDK file_path；不是直接填写 COM 端口。"
            : type == 54 ? "Bluetooth：先按厂商工具完成配对与总线参数配置。"
            : type is 32 or 37 ? "CANDTU：按 SDK 在 InitCAN 后设置标准波特率。"
            : type is 78 or 79 ? "ZPSCANFD：按 SDK 在 InitCAN 后设置波特率和终端电阻。"
            : type == 99 ? "Virtual：由 SDK 虚拟设备实现，无物理总线参数。"
            : "本地设备：按型号对应的 SDK 初始化接口配置。";
        if (UsesExternalBusConfiguration(type)) family += " 总线波特率、采样点及终端电阻由设备/配置工具管理，本步骤不宣称已修改。";
        if (IsAdditionalUsbFd(type) || IsPostConfigured(type) || type is 19 or 22 or 31 or 34 or 45)
            family += " 使用驱动标准波特率；采样点由 SDK 决定，本步骤不应用采样点输入。";
        var device = Devices.FirstOrDefault(item => item.Type == type);
        return family + (string.IsNullOrEmpty(device?.KernelLibrary)
            ? " 当前 SDK 无该型号的内核映射，需厂商提供匹配实现后使用。"
            : $" 所需内核：{device.KernelLibrary}；型号列出不代表本机已安装驱动。错误详情会给出缺少的依赖。");
    }
}