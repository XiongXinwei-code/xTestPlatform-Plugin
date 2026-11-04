using System.IO;
using System.Text.RegularExpressions;
using CAN.Adapters.Zlg;
using Xunit;

namespace CAN.ContractTests;

public sealed class ZlgHardwareCatalogTests
{
    internal static string Root()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "CAN/CanPlugin/Native/Zlg/x64/zlgcan.h"))) return dir.FullName;
        throw new DirectoryNotFoundException("Repository SDK header not found");
    }

    [Fact]
    public void Every_device_constant_in_the_bundled_SDK_header_is_recognized()
    {
        string header = File.ReadAllText(Path.Combine(Root(), "CAN/CanPlugin/Native/Zlg/x64/zlgcan.h"));
        header = header[..header.IndexOf("#define ZCAN_ERROR_CAN_OVERFLOW", StringComparison.Ordinal)];
        var constants = Regex.Matches(header, @"(?m)^#define\s+(ZCAN_\w+)\s+(\d+)\s*$");
        Assert.True(constants.Count > 80);
        foreach (Match constant in constants)
            Assert.Equal(uint.Parse(constant.Groups[2].Value), ZlgApi.ParseDeviceType(constant.Groups[1].Value));
    }

    [Theory]
    [InlineData(48u)] [InlineData(50u)] [InlineData(54u)] [InlineData(59u)] [InlineData(76u)] [InlineData(78u)] [InlineData(87u)]
    public void Network_Bluetooth_extended_USB_and_special_FD_models_are_not_misclassified_as_Classic(uint type)
        => Assert.True(ZlgApi.IsCanFdDevice(type));
}
