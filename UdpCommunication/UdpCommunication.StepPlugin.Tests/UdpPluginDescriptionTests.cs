using System.Collections;
using System.Resources;
using UdpCommunication.StepPlugin.Models;
using UdpCommunication.StepPlugin.UI;
using Xunit;

namespace UdpCommunication.StepPlugin.Tests;

public sealed class UdpPluginDescriptionTests
{
    private const string UdpIconPath = "pack://application:,,,/UdpCommunication.StepPlugin.UI;component/Resources/Icons/udp.png";

    [Fact]
    public void UdpPlugins_UseTheSharedUdpIcon()
    {
        Assert.Equal(UdpIconPath, new UdpSendPlugin().IconPath);
        Assert.Equal(UdpIconPath, new UdpSendAndReceivePlugin().IconPath);
        Assert.Equal(UdpIconPath, new UdpSendEditorPlugin().IconPath);
        Assert.Equal(UdpIconPath, new UdpSendAndReceiveEditorPlugin().IconPath);
    }

    [Fact]
    public void UdpEditor_UsesTheUdpIconAndFunctionalTabName()
    {
        var repositoryRoot = FindRepositoryRoot();
        var iconPath = Path.Combine(repositoryRoot, "UdpCommunication", "UdpCommunication.StepPlugin.UI", "Resources", "Icons", "udp.png");
        var xamlPath = Path.Combine(repositoryRoot, "UdpCommunication", "UdpCommunication.StepPlugin.UI", "Views", "UdpEditorView.xaml");

        Assert.True(File.Exists(iconPath));
        var xaml = File.ReadAllText(xamlPath);
        Assert.Contains("Header=\"UDP\"", xaml, StringComparison.Ordinal);
        Assert.Contains($"Image=\"{UdpIconPath}\"", xaml, StringComparison.Ordinal);
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

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "xTestPlatform_StepPlugin_Development_Guide.md")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
