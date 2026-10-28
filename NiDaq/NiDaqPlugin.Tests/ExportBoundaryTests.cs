using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using NiDaq.Helpers;
using Xunit;

namespace NiDaq.ContractTests;

public sealed class ExportBoundaryTests
{
    [Fact]
    public async Task RotationLogCannotSubmitFileMutationAfterExportDeadline()
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "DaqTestData");
        Directory.CreateDirectory(directory);
        string name = Guid.NewGuid().ToString("N");
        string path = Path.Combine(directory, name + ".csv");
        File.WriteAllText(path, "AI\n" + new string('0', 1024 * 1024));
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var owner = new DaqTaskResource(() => { }, () => { });
        await owner.ConfigureAsync(() => { }, default);
        var exporting = owner.RunAsync(() =>
        {
            DaqFileWriter.AppendCsv(path, new double[,] { { 1 } }, ["AI"], 1,
                _ => { entered.TrySetResult(); release.Wait(); }, owner.EnsureOperationActive);
            return true;
        }, 100, "CSV 轮转", default);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await Assert.ThrowsAsync<TimeoutException>(() => exporting);
        }
        finally { release.Set(); await owner.CloseAsync(2000, default); }
        Assert.Empty(Directory.GetFiles(directory, name + "_*.csv"));
        Assert.True(new FileInfo(path).Length > 1024 * 1024);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExportTimeoutIsEditableAndSurvivesImmediateRefresh(bool sync) => UiThread.Run(() =>
    {
        var serializer = sync ? new NiDaqSyncReadPlugin().CreateSerializer() : new NiDaqAiReadPlugin().CreateSerializer();
        var step = new xTestPlatform.Core.SequenceModels.Step();
        step.StepSetting.Setting = serializer.Serialize(serializer.Deserialize([0x80], 1));
        step.StepSetting.SettingVersion = serializer.SettingVersion;
        var page = sync ? (System.Windows.Controls.UserControl)new NiDaq.UI.Views.NiDaqSyncReadEditorView()
            : new NiDaq.UI.Views.NiDaqAiReadEditorView();
        page.GetType().GetMethod("RefreshFromStep")!.Invoke(page, [step]);
        page.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.DataBind);
        page.Measure(new System.Windows.Size(1000, 750));
        page.Arrange(new System.Windows.Rect(0, 0, 1000, 750));
        page.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.DataBind);
        var box = Assert.IsType<System.Windows.Controls.TextBox>(page.FindName("ExportTimeoutBox"));
        box.Text = "137";
        box.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)!.UpdateSource();
        page.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.FrameworkElement.UnloadedEvent));
        object saved = serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
        Assert.Equal(137, (int)saved.GetType().GetProperty("ExportTimeoutMs")!.GetValue(saved)!);
    });

    [Fact]
    public async Task ExpiredExportWaitingForFileLockNeverWritesAfterLockIsReleased()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "DaqTestData", Guid.NewGuid().ToString("N") + ".csv");
        var gates = (ConcurrentDictionary<string, object>)typeof(DaqFileWriter)
            .GetField("FileGates", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        var fileGate = gates.GetOrAdd(path, _ => new object());
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var holder = Task.Run(() => { lock (fileGate) { entered.TrySetResult(); release.Wait(); } });
        var owner = new DaqTaskResource(() => { }, () => { });
        await owner.ConfigureAsync(() => { }, default);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await Assert.ThrowsAsync<TimeoutException>(() => owner.RunAsync(() =>
            {
                DaqFileWriter.AppendCsv(path, new double[,] { { 1 } }, ["AI"], 1, null,
                    owner.EnsureOperationActive);
                return true;
            }, 30, "CSV 导出", default));
        }
        finally { release.Set(); await holder; await owner.CloseAsync(2000, default); }
        Assert.False(File.Exists(path));
    }
}
