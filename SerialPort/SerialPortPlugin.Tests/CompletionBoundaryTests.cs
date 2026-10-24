using SerialPort.Helpers;
using SerialPort.Models;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using Xunit;

namespace SerialPort.Tests;

public sealed class CompletionBoundaryTests
{
    [Fact]
    public async Task Every_step_honors_an_already_cancelled_caller_before_resource_lookup_or_io()
    {
        using var caller = new CancellationTokenSource(); caller.Cancel();
        IStepPlugin[] plugins = [new SerialPortOpenPlugin(), new SerialPortClosePlugin(), new SerialPortReadPlugin(), new SerialPortWritePlugin(), new SerialPortQueryPlugin()];
        var failed = new List<string>();
        foreach (var plugin in plugins)
        {
            var (context, _) = PublicationContextProxy.Create(plugin, plugin.CreateSerializer().CreateDefault());
            var result = await plugin.CreateExecutor().ExecuteAsync(context, caller.Token).WaitAsync(TimeSpan.FromSeconds(15));
            if (result.StepResult.Status != TestStatus.Aborted) failed.Add(plugin.StepTypeId + ": " + result.StepResult.Status);
        }
        Assert.Empty(failed);
    }

    [Fact]
    public async Task Null_data_expression_cannot_send_the_literal_word_null()
    {
        var channel = new Channel(); var owner = new SerialConnectionResource(channel);
        var plugin = new SerialPortWritePlugin(); var (context, state) = PublicationContextProxy.Create(plugin, new SerialPortWriteSetting { WriteData = "null" });
        state.Store.Values[SerialPortHelper.GetPortKey("COM1")] = owner;
        try
        {
            var result = await plugin.CreateExecutor().ExecuteAsync(context).WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Equal(TestStatus.Error, result.StepResult.Status);
            Assert.Equal(0, channel.Writes);
        }
        finally { await owner.CloseAsync(2000, default); }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancellation_during_log_cannot_be_reported_as_completed(bool close)
    {
        using var caller = new CancellationTokenSource();
        var channel = new Channel(); var owner = new SerialConnectionResource(channel);
        IStepPlugin plugin = close ? new SerialPortClosePlugin() : new SerialPortWritePlugin();
        object setting = close ? new SerialPortCloseSetting { PortName = "\"COM1\"" } : new SerialPortWriteSetting { PortName = "\"COM1\"", WriteData = "\"x\"" };
        var (context, state) = PublicationContextProxy.Create(plugin, setting);
        state.Store.Values[SerialPortHelper.GetPortKey("COM1")] = owner;
        state.OnLog = caller.Cancel;
        try
        {
            var result = await plugin.CreateExecutor().ExecuteAsync(context, caller.Token).WaitAsync(TimeSpan.FromSeconds(15));
            Assert.True(caller.IsCancellationRequested);
            Assert.Equal(TestStatus.Aborted, result.StepResult.Status);
            Assert.Equal(close ? 0 : 1, channel.Writes);
        }
        finally { await owner.CloseAsync(2000, default); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Unknown_format_is_rejected_not_silently_sent_as_utf8(bool read)
    {
        var unknown = (SerialPortDataFormat)999;
        Assert.Throws<ArgumentOutOfRangeException>(() => read ? SerialPortHelper.ConvertFromBytes([0x42], unknown) : Convert.ToHexString(SerialPortHelper.ConvertToBytes("B", unknown)));
    }
    private sealed class Channel : ISerialChannel
    {
        internal int Writes;
        public bool IsOpen { get; private set; } = true;
        public int ReadTimeout { get; set; } = 2000;
        public int WriteTimeout { get; set; } = 2000;
        public int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public void Write(byte[] bytes) => Writes++;
        public void Dispose() => IsOpen = false;
    }
}
