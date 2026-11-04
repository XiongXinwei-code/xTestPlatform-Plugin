using System.Buffers;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using CAN.Models;
using CAN.Runtime;
using CAN.UI.Views;
using MessagePack;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Models;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class CanFdCyclicTests
{
    [Fact]
    public void Old_serialized_rows_remain_classic()
    {
        var buffer = new ArrayBufferWriter<byte>(); var writer = new MessagePackWriter(buffer);
        writer.WriteMapHeader(3);
        writer.Write("ConnectionName"); writer.Write("\"CAN1\"");
        writer.Write("TaskName"); writer.Write("\"old\"");
        writer.Write("Messages"); writer.WriteArrayHeader(1); writer.WriteMapHeader(5);
        writer.Write("CanId"); writer.Write("\"0x201\"");
        writer.Write("FrameType"); writer.Write(0);
        writer.Write("Data"); writer.Write("\"01\"");
        writer.Write("CycleTimeMs"); writer.Write(10);
        writer.Write("Enabled"); writer.Write(true); writer.Flush();
        var bytes = buffer.WrittenSpan.ToArray();
        var setting = (CanCyclicSendStartSetting)new CanCyclicSendStartPlugin().CreateSerializer().Deserialize(bytes, 1);
        Assert.False(Assert.Single(setting.Messages).UseFdFrame);
        Assert.Equal(10, setting.Messages[0].CycleTimeMs);
        Assert.True(setting.Messages[0].Enabled);
        Assert.Equal("", setting.Messages[0].CycleTimeMsExpression);
        Assert.Equal("", setting.Messages[0].EnabledExpression);
        Assert.Equal(20, setting.ControlRefreshMs);
        Assert.Equal(5000, setting.ExpressionTimeoutMs);
    }

    [WpfFact]
    public void Editor_fd_checkbox_saves_and_reloads_the_selected_row()
    {
        var plugin = new CanCyclicSendStartPlugin(); var serializer = plugin.CreateSerializer();
        var step = new xTestPlatform.Core.SequenceModels.Step();
        step.StepSetting.Setting = serializer.Serialize(new CanCyclicSendStartSetting { Messages = [new()] });
        step.StepSetting.SettingVersion = serializer.SettingVersion;
        var view = new CanCyclicSendStartEditorView(); view.RefreshFromStep(step);
        view.Measure(new Size(900, 500)); view.Arrange(new Rect(0, 0, 900, 500)); view.UpdateLayout();
        var grid = Assert.IsType<DataGrid>(view.FindName("MessagesGrid"));
        var column = Assert.Single(grid.Columns.OfType<DataGridCheckBoxColumn>(), c => Equals(c.Header, "CAN FD/BRS"));
        var row = Assert.Single(view.ViewModel.Messages);
        var check = Assert.IsType<CheckBox>(column.GetCellContent(row));
        check.IsChecked = true; view.ViewModel.Flush();
        var saved = (CanCyclicSendStartSetting)serializer.Deserialize(step.StepSetting.Setting, serializer.SettingVersion);
        Assert.True(Assert.Single(saved.Messages).UseFdFrame);
        view.RefreshFromStep(step); Assert.True(Assert.Single(view.ViewModel.Messages).UseFdFrame);
    }

    [Fact]
    public async Task Mixed_classic_and_fd_messages_keep_format_and_payload_and_stop()
    {
        var fdData = Enumerable.Range(0, 64).Select(i => (byte)i).ToArray();
        var rows = new[]
        {
            new CyclicMessageItem { CanId = "\"0x123\"", Data = "\"01 02\"", CycleTimeMs = 5 },
            new CyclicMessageItem { CanId = "\"0x1ABCDE\"", FrameType = CanFrameType.Extended,
                Data = $"\"{Convert.ToHexString(fdData)}\"", CycleTimeMs = 5, UseFdFrame = true }
        };
        var (owner, raw, context, state) = Setup(rows, CanProtocolType.FD);
        try
        {
            Assert.Equal(TestStatus.Passed, (await state.Plugin!.CreateExecutor().ExecuteAsync(context)).StepResult.Status);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (raw.Sent.Select(x => x.Id).Distinct().Count() < 2) await Task.Delay(5, timeout.Token);
            await owner.StopJobAsync("CyclicTask1", 1000, default);
            var classic = raw.Sent.First(x => x.Id == 0x123);
            var fd = raw.Sent.First(x => x.Id == 0x1ABCDE);
            Assert.False(classic.IsFd); Assert.Equal(new byte[] { 1, 2 }, classic.Data);
            Assert.True(fd.IsFd); Assert.Equal(CanFrameType.Extended, fd.FrameType); Assert.Equal(fdData, fd.Data);
            var count = raw.Sent.Count; await Task.Delay(30); Assert.Equal(count, raw.Sent.Count);
        }
        finally { await owner.CloseAsync(); }
    }

    [Theory]
    [InlineData(false, 12)]
    [InlineData(true, 65)]
    public async Task Invalid_fd_request_never_reaches_driver(bool fdChannel, int length)
    {
        var row = new CyclicMessageItem { CanId = "\"0x123\"", Data = $"\"{new string('0', length * 2)}\"", CycleTimeMs = 5, UseFdFrame = true };
        var (owner, raw, context, state) = Setup([row], fdChannel ? CanProtocolType.FD : CanProtocolType.Classic);
        try
        {
            var result = await state.Plugin!.CreateExecutor().ExecuteAsync(context);
            if (!fdChannel)
            {
                Assert.Equal(TestStatus.Error, result.StepResult.Status); Assert.Empty(raw.Sent); return;
            }
            // Pool updates validate every row before taking over any existing ID.
            Assert.Equal(TestStatus.Error, result.StepResult.Status);
            Assert.Empty(raw.Sent); Assert.Empty(owner.GetCyclicStatuses());
        }
        finally { await Record.ExceptionAsync(() => owner.CloseAsync()); Assert.True(owner.IsClosed); }
    }

    private static (CanConnectionResource Owner, FakeAdapter Raw, IExecutionContext Context, CloseContext State) Setup(CyclicMessageItem[] rows, CanProtocolType protocol)
    {
        var context = DispatchProxy.Create<IExecutionContext, CloseContext>(); var state = (CloseContext)(object)context;
        var raw = new FakeAdapter(); var owner = new CanConnectionResource(raw); owner.Open(new() { Protocol = protocol }); state.Adapter = owner;
        state.Plugin = new CanCyclicSendStartPlugin(); state.Setting = new CanCyclicSendStartSetting { Messages = new(rows) };
        return (owner, raw, context, state);
    }
}
