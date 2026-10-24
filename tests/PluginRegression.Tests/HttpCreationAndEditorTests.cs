using System.Collections;
using System.ComponentModel;
using System.Net.Http;
using System.Reflection;
using Http;
using Http.Helpers;
using Http.Models;
using Http.UI.ViewModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using Xunit;

namespace PluginRegression.Tests;

public sealed class HttpCreationAndEditorTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Header_expression_cells_use_the_standard_editor_and_parent_sequence_context(int kind) => EthernetDeadlineUiThread.Run(() =>
    {
        (IStepPlugin plugin, IStepEditorPlugin editor) = kind switch
        {
            0 => ((IStepPlugin)new HttpClientCreatePlugin(), (IStepEditorPlugin)new Http.UI.HttpClientCreateEditorPlugin()),
            1 => ((IStepPlugin)new HttpRequestPlugin(), (IStepEditorPlugin)new Http.UI.HttpRequestEditorPlugin()),
            _ => ((IStepPlugin)new HttpSoapRequestPlugin(), (IStepEditorPlugin)new Http.UI.HttpSoapRequestEditorPlugin())
        };
        var (_, state) = TestContext.Create(plugin, plugin.CreateSerializer().CreateDefault());
        var sequence = new xTestPlatform.Core.SequenceModels.SequenceFile();
        var view = editor.CreateEditor(state.Current.Step, sequence);
        var vm = view.DataContext;
        var rows = (IList)vm.GetType().GetProperty(kind == 0 ? "DefaultHeaders" : "Headers")!.GetValue(vm)!;
        var item = new HttpHeaderItem { Name = "X-Test", Value = "\"initial\"" };
        rows.Add(item);
        var window = new System.Windows.Window { Content = view, Width = 1100, Height = 1200,
            Left = -30000, Top = -30000, ShowActivated = false, ShowInTaskbar = false, Opacity = 0 };
        try
        {
            window.Show(); window.UpdateLayout();
            var expressions = Descendants(view).OfType<System.Windows.FrameworkElement>()
                .Where(x => x.GetType().Name == "ExpressionTextBox" && ReferenceEquals(x.DataContext, item)).ToArray();
            Assert.NotEmpty(expressions);
            foreach (var expression in expressions)
            {
                Assert.Same(sequence, expression.GetType().GetProperty("SequenceFile")!.GetValue(expression));
                Assert.Equal("String", expression.GetType().GetProperty("ExpectedResultType")!.GetValue(expression)!.ToString());
            }
        }
        finally { window.Close(); }
    });

    private static IEnumerable<System.Windows.DependencyObject> Descendants(System.Windows.DependencyObject parent)
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Late_builder_result_is_never_published_and_only_its_own_client_is_disposed(bool cancel)
    {
        using var guard = new CancellationTokenSource(10000);
        using var caller = new CancellationTokenSource();
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var oldHandler = new DisposalHandler();
        var oldClient = new HttpClient(oldHandler);
        var building = HttpHelper.BuildClientAsync(() =>
        {
            entered.TrySetResult();
            if (!release.Wait(10000)) throw new TimeoutException("builder barrier");
            return oldClient;
        }, cancel ? 5000 : 50, caller.Token);
        try
        {
            await entered.Task.WaitAsync(guard.Token);
            if (cancel) { caller.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => building); }
            else await Assert.ThrowsAsync<TimeoutException>(() => building);
            var currentHandler = new DisposalHandler();
            using var current = await HttpHelper.BuildClientAsync(() => new HttpClient(currentHandler), 1000, guard.Token);
            release.Set();
            await oldHandler.Disposed.Task.WaitAsync(guard.Token);
            Assert.Equal(1, oldHandler.DisposeCount);
            Assert.Equal(0, currentHandler.DisposeCount);
        }
        finally { release.Set(); }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Clearing_editor_collection_unsubscribes_removed_items_and_cannot_schedule_a_late_save(int kind) => EthernetDeadlineUiThread.Run(() =>
    {
        var (plugin, vm, collection) = kind switch
        {
            0 => ((IStepPlugin)new HttpClientCreatePlugin(), (object)new HttpClientCreateViewModel(), "DefaultHeaders"),
            1 => (new HttpRequestPlugin(), (object)new HttpRequestViewModel(), "Headers"),
            2 => (new HttpSoapRequestPlugin(), (object)new HttpSoapRequestViewModel(), "Headers"),
            3 => (new HttpJsonExtractPlugin(), (object)new HttpJsonExtractViewModel(), "Items"),
            _ => (new HttpXmlExtractPlugin(), (object)new HttpXmlExtractViewModel(), "Items")
        };
        var serializer = DispatchProxy.Create<IStepSettingSerializer, CountingSerializer>();
        var probe = (CountingSerializer)(object)serializer;
        probe.Inner = plugin.CreateSerializer();
        var (_, state) = TestContext.Create(plugin, probe.Inner.CreateDefault());
        vm.GetType().GetMethod("AttachSerializer")!.Invoke(vm, [serializer]);
        vm.GetType().GetMethod("AttachStep")!.Invoke(vm, [state.Current.Step]);
        var items = (IList)vm.GetType().GetProperty(collection)!.GetValue(vm)!;
        var item = collection == "Items" ? (INotifyPropertyChanged)new HttpExtractItem() : new HttpHeaderItem();
        items.Add(item);
        items.Clear();
        vm.GetType().GetMethod("FlushPendingSave")!.Invoke(vm, null);
        int writes = probe.Writes;
        item.GetType().GetProperty(collection == "Items" ? "DefaultValue" : "Value")!.SetValue(item, "removed row changed");
        vm.GetType().GetMethod("FlushPendingSave")!.Invoke(vm, null);
        Assert.Equal(writes, probe.Writes);
    });

    private sealed class DisposalHandler : HttpMessageHandler
    {
        internal int DisposeCount;
        internal readonly TaskCompletionSource Disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override void Dispose(bool disposing) { Interlocked.Increment(ref DisposeCount); Disposed.TrySetResult(); base.Dispose(disposing); }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => throw new NotSupportedException();
    }
}

public class CountingSerializer : DispatchProxy
{
    public IStepSettingSerializer Inner { get; set; } = null!;
    public int Writes { get; private set; }
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method!.Name == "Serialize") Writes++;
        return method.Invoke(Inner, args);
    }
}
