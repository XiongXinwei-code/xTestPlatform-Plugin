using NiDaq.Helpers;
using NiDaq.Models;
using Xunit;

namespace NiDaq.ContractTests;
public class DaqDataTests
{
    [Theory]
    [InlineData(DaqExportFormat.Tdms)]
    [InlineData(DaqExportFormat.TdmsAndVariable)]
    public void UnsupportedExportCannotSilentlyBecomeCsv(DaqExportFormat format)
        => Assert.Throws<NotSupportedException>(() => DaqExportPolicy.Validate(format, false));
    [Fact]
    public void SyncRequiresARealClockPathAndOneHardwareDevice()
    {
        var setting = new NiDaqSyncConfigSetting();
        setting.AiChannels.Add(new NiDaqAiChannel { PhysicalChannel = "Dev1/ai0" });
        setting.EncoderChannels.Add(new NiDaqSyncEncoderChannel { CounterChannel = "Dev2/ctr0" });
        Assert.Throws<NotSupportedException>(() => DaqSamplePlan.Create(setting));
    }

    [Fact]
    public void SyncPlanUsesAiSampleClockForEveryIndependentCounter()
    {
        var setting = new NiDaqSyncConfigSetting();
        setting.AiChannels.Add(new NiDaqAiChannel { PhysicalChannel = "Dev1/ai0" });
        setting.EncoderChannels.Add(new NiDaqSyncEncoderChannel { CounterChannel = "Dev1/ctr0" });
        setting.EncoderChannels.Add(new NiDaqSyncEncoderChannel { CounterChannel = "Dev1/ctr1" });
        var plan = DaqSamplePlan.Create(setting);
        Assert.Equal("/Dev1/ai/SampleClock", plan.CounterClockSource);
        Assert.Equal(2, plan.CounterCount);
    }
}
