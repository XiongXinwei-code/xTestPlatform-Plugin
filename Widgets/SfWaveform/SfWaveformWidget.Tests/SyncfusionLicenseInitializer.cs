using System.Runtime.CompilerServices;
using Syncfusion.Licensing;

namespace xTestPlatformDev.SfWaveform.Tests;

internal static class SyncfusionLicenseInitializer
{
    internal static bool IsRegistered { get; private set; }

    [ModuleInitializer]
    internal static void Initialize()
    {
        var licenseKey = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");
        if (!string.IsNullOrWhiteSpace(licenseKey))
        {
            SyncfusionLicenseProvider.RegisterLicense(licenseKey);
            IsRegistered = true;
        }
    }
}
