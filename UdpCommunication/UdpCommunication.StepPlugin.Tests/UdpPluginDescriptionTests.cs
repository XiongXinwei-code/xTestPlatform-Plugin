using System.Collections;
using System.Resources;
using UdpCommunication.Models;
using UdpCommunication.UI;
using UdpCommunication.UI.Editors;
using Xunit;

namespace UdpCommunication.Tests;

public sealed class UdpPluginDescriptionTests
{
    private const string UdpIconPath = "pack://application:,,,/UdpCommunication.StepPlugin.UI;component/Resources/Icons/udp.png";

    [Fact]
    public void UdpPlugins_UseTheSharedUdpIcon()
    {
        Assert.Equal(UdpIconPath, new UdpOpenPlugin().IconPath);
        Assert.Equal(UdpIconPath, new UdpClosePlugin().IconPath);
        Assert.Equal(UdpIconPath, new UdpSendPlugin().IconPath);
        Assert.Equal(UdpIconPath, new UdpReceivePlugin().IconPath);
        Assert.Equal(UdpIconPath, new UdpSendAndReceivePlugin().IconPath);
        Assert.Equal(UdpIconPath, new UdpOpenEditorPlugin().IconPath);
        Assert.Equal(UdpIconPath, new UdpCloseEditorPlugin().IconPath);
        Assert.Equal(UdpIconPath, new UdpSendEditorPlugin().IconPath);
        Assert.Equal(UdpIconPath, new UdpReceiveEditorPlugin().IconPath);
        Assert.Equal(UdpIconPath, new UdpSendAndReceiveEditorPlugin().IconPath);
    }

    [Fact]
    public void UdpEditorAssembly_EmbedsTheUdpIconResource()
    {
        var assembly = typeof(UdpSendEditorPlugin).Assembly;
        using var stream = assembly.GetManifestResourceStream($"{assembly.GetName().Name}.g.resources");
        Assert.NotNull(stream);

        using var reader = new ResourceReader(stream!);
        var resourceNames = reader.Cast<DictionaryEntry>()
            .Select(entry => (string)entry.Key)
            .ToArray();

        Assert.Contains("resources/icons/udp.png", resourceNames, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void SendDescription_IncludesPayloadPreview()
    {
        var plugin = new UdpSendPlugin();
        var setting = new UdpSendSetting { RemotePort = 9000, RequestData = "PING" };

        var description = plugin.GenerateDescription(plugin.CreateSerializer().Serialize(setting));

        Assert.Contains("PING", description, StringComparison.Ordinal);
    }

    [Fact]
    public void SendAndReceiveDescription_IncludesExpectedReplyPreview()
    {
        var plugin = new UdpSendAndReceivePlugin();
        var setting = new UdpSendAndReceiveSetting { RemotePort = 9000, RequestData = "PING", ExpectedReply = "ACK" };

        var description = plugin.GenerateDescription(plugin.CreateSerializer().Serialize(setting));

        Assert.Contains("ACK", description, StringComparison.Ordinal);
    }
}
