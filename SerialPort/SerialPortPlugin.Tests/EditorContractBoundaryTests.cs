using SerialPort.Helpers;
using SerialPort.Models;
using SerialPort.UI;
using StepEditor.Abstractions;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using xTestPlatform.Core.Services.ExpressionEngine;
using Xunit;

namespace SerialPort.Tests;

public sealed class EditorContractBoundaryTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    [InlineData(-1)]
    public async Task Open_io_deadline_validation_matches_supported_infinite_and_invalid_values(int timeout)
    {
        var errors = await new SerialPortOpenEditorPlugin().ValidateWithContextAsync(ValidationContext(new SerialPortOpenPlugin(),
            new SerialPortOpenSetting { ReadTimeoutMs = timeout, WriteTimeoutMs = timeout }));
        if (timeout == -1) Assert.DoesNotContain(errors, error => error.Code is "SP_006" or "SP_007");
        else foreach (string code in new[] { "SP_006", "SP_007" }) Assert.Contains(errors, error => error.Code == code && error.Severity.ToString() == "Error");
    }

    [Fact]
    public async Task Unsupported_stopbits_none_is_rejected_before_open()
    {
        var errors = await new SerialPortOpenEditorPlugin().ValidateWithContextAsync(ValidationContext(new SerialPortOpenPlugin(), new SerialPortOpenSetting { StopBits = 0 }));
        Assert.Contains(errors, error => error.Code == "SP_004" && error.Severity.ToString() == "Error");
    }

    [Fact]
    public async Task Query_checks_both_expression_syntax_fields()
    {
        var errors = await new SerialPortQueryEditorPlugin().ValidateWithContextAsync(ValidationContext(new SerialPortQueryPlugin(),
            new SerialPortQuerySetting { PortName = "(", WriteData = "(", ResultVariable = "Locals.rx" }));
        Assert.Contains(errors, error => error.Code == "SP_040E");
        Assert.Contains(errors, error => error.Code == "SP_041E");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Infinite_read_without_frame_end_is_rejected_and_hex_literals_are_not_misparsed_as_raw_expressions(bool query)
    {
        IStepPlugin plugin = query ? new SerialPortQueryPlugin() : new SerialPortReadPlugin();
        IStepEditorPlugin editor = query ? new SerialPortQueryEditorPlugin() : new SerialPortReadEditorPlugin();
        object setting = query
            ? new SerialPortQuerySetting { WriteData = "\"01\"", DataFormat = SerialPortDataFormat.Hex, ReadTimeoutMs = -1, ReadBytes = 0, Terminator = "", ResultVariable = "Locals.rx" }
            : new SerialPortReadSetting { ReadTimeoutMs = -1, ReadBytes = 0, Terminator = "", ResultVariable = "Locals.rx" };
        var errors = await editor.ValidateWithContextAsync(ValidationContext(plugin, setting));
        Assert.Contains(errors, error => error.Message.Contains("无限读取", StringComparison.Ordinal));
        Assert.DoesNotContain(errors, error => error.Code == "SP_046");
    }

    [Fact]
    public async Task Invalid_read_format_does_not_consume_a_response()
    {
        var channel = new Channel(); var owner = new SerialConnectionResource(channel);
        var plugin = new SerialPortReadPlugin();
        var (context, state) = PublicationContextProxy.Create(plugin, new SerialPortReadSetting { DataFormat = (SerialPortDataFormat)999, ReadBytes = 1, Terminator = "", ResultVariable = "Locals.rx" });
        state.Store.Values[SerialPortHelper.GetPortKey("COM1")] = owner;
        try
        {
            var result = await plugin.CreateExecutor().ExecuteAsync(context).WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Equal(TestStatus.Error, result.StepResult.Status);
            Assert.Equal(0, channel.Reads);
        }
        finally { await owner.CloseAsync(2000, default); }
    }

    private static StepEditorValidationContext ValidationContext(IStepPlugin plugin, object setting)
    {
        var (context, _) = PublicationContextProxy.Create(plugin, setting); var step = context.CurrentStep!.Step;
        return new StepEditorValidationContext { Setting = step.StepSetting.Setting, ExecutionContext = context, Evaluator = ExpressionEvaluatorFactory.Default,
            CurrentStep = step, SequenceFile = new SequenceFile(), Block = [step] };
    }
    private sealed class Channel : ISerialChannel
    {
        internal int Reads;
        public bool IsOpen { get; private set; } = true;
        public int ReadTimeout { get; set; } = 2000;
        public int WriteTimeout { get; set; } = 2000;
        public int Read(byte[] buffer, int offset, int count) { Reads++; buffer[offset] = 0x42; return 1; }
        public void Write(byte[] bytes) { }
        public void Dispose() => IsOpen = false;
    }
}
