using System.Diagnostics;
using System.Reflection;
using Xunit;

namespace UdpCommunication.StepPlugin.Tests;

public sealed class PluginDeploymentTests
{
    [Fact]
    public async Task PublishScript_CreatesDeployablePackageWithoutHostAssemblies()
    {
        var repositoryRoot = FindRepositoryRoot();
        var script = Path.Combine(repositoryRoot, "UdpCommunication", "build", "Publish-Plugin.ps1");
        Assert.True(File.Exists(script), "缺少插件部署脚本");

        var outputDirectory = Path.Combine(Path.GetTempPath(), $"udp-plugin-{Guid.NewGuid():N}");
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\" -Configuration Release -OutputDirectory \"{outputDirectory}\"",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            })!;
            var standardOutputTask = process.StandardOutput.ReadToEndAsync();
            var standardErrorTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var standardError = await standardErrorTask;
            Assert.True(process.ExitCode == 0, standardError);
            await standardOutputTask;

            Assert.True(File.Exists(Path.Combine(outputDirectory, "UdpCommunication.StepPlugin.dll")));
            Assert.True(File.Exists(Path.Combine(outputDirectory, "UdpCommunication.StepPlugin.UI.dll")));
            Assert.True(File.Exists(Path.Combine(outputDirectory, "Microsoft.NET.StringTools.dll")));
            Assert.False(File.Exists(Path.Combine(outputDirectory, "xTestPlatform.Core.dll")));
            Assert.False(File.Exists(Path.Combine(outputDirectory, "xTestPlatform.StepEditor.SDK.dll")));
            Assert.Equal(new Version(32, 1, 25, 0), AssemblyName.GetAssemblyName(Path.Combine(outputDirectory, "Syncfusion.Tools.WPF.dll")).Version);
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, true);
            }
        }
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

        throw new DirectoryNotFoundException("未找到项目根目录");
    }
}
