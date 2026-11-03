using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using CAN.Dbc;
using CAN.Runtime;
using CAN.UI.Dbc;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace CAN.Tests;

public sealed class DbcSharedCatalogTests
{
    private const string Text = "BO_ 291 Good: 1 ECU\n SG_ A : 0|8@1+ (1,0) [0|255] \"\" ECU";

    [Fact]
    public async Task Equal_contents_from_many_steps_reuse_one_parsed_object()
    {
        int parses = 0;
        var store = new DbcCatalogStore((text, ct) =>
        {
            Interlocked.Increment(ref parses);
            return DbcDatabase.ParseCatalog(text, ct);
        });
        var results = await Task.WhenAll(Enumerable.Range(0, 32)
            .Select(_ => store.LoadAsync(new string(Text.ToCharArray()), CancellationToken.None)));
        Assert.Equal(1, parses);
        Assert.All(results, result => Assert.Same(results[0].Database, result.Database));
        Assert.Same(results[0].Database, (await store.LoadAsync(Text, CancellationToken.None)).Database);
        Assert.NotSame(results[0].Database, (await store.LoadAsync(Text.Replace("Good", "More"), CancellationToken.None)).Database);
        Assert.Equal(2, parses);
    }

    [WpfFact]
    public async Task Editors_and_execution_keep_separate_settings_around_the_same_message_definition()
    {
        var plugin = new DbcWritePlugin();
        var serializer = plugin.CreateSerializer();
        var editors = new List<DbcEditorViewModel>();
        try
        {
            for (int i = 0; i < 12; i++)
            {
                var setting = new DbcWriteSetting
                {
                    Document = new() { Text = Text },
                    Messages = [new() { MessageId = 291, Signals = [new() { Name = "A", Value = i.ToString() }] }]
                };
                var step = new Step(); step.StepSetting.StepType = plugin.StepTypeId;
                step.StepSetting.Setting = serializer.Serialize(setting);
                step.StepSetting.SettingVersion = serializer.SettingVersion;
                var model = new DbcEditorViewModel(plugin); editors.Add(model); model.Attach(step);
            }
            await Task.WhenAll(editors.Select(m => m.LoadTask));
            var context = DispatchProxy.Create<IExecutionContext, DbcTestContext>();
            var run = await DbcCatalogCache.LoadAsync(new() { Text = Text }, context, new(context, CancellationToken.None),
                CancellationToken.None);
            var definition = run.Database.Find(291, false);
            for (int i = 0; i < editors.Count; i++)
            {
                Assert.Same(definition, Assert.Single(editors[i].Messages).Message);
                Assert.Equal(i.ToString(), Assert.Single(editors[i].SignalRows).Value);
            }
            editors[0].SignalRows[0].Value = "73";
            Assert.Equal("1", editors[1].SignalRows[0].Value);
        }
        finally { foreach (var model in editors) model.Deactivate(); }
    }

    [Fact]
    public async Task Live_directory_identity_survives_many_other_documents()
    {
        var store = new DbcCatalogStore();
        var original = await store.LoadAsync(Text, CancellationToken.None);
        var owners = new List<DbcDatabase>();
        for (int i = 0; i < 140; i++)
            owners.Add((await store.LoadAsync(Text.Replace("Good", $"Other{i}"), CancellationToken.None)).Database);
        Assert.Same(original.Database, (await store.LoadAsync(new string(Text.ToCharArray()), CancellationToken.None)).Database);
        GC.KeepAlive(owners);
    }

    [Fact]
    public async Task Parsed_catalog_is_collectible_after_last_owner_releases_it()
    {
        var store = new DbcCatalogStore();
        var weak = await GetWeakCatalog(store);
        for (int i = 0; i < 3 && weak.IsAlive; i++) GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
        Assert.False(weak.IsAlive);
        GC.KeepAlive(store);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<WeakReference> GetWeakCatalog(DbcCatalogStore store)
        => new((await store.LoadAsync(Text, CancellationToken.None)).Database);

    [Fact]
    public async Task Shared_worker_does_not_capture_a_callers_execution_context()
    {
        var sequence = new AsyncLocal<string?> { Value = "old execution" };
        try
        {
            var jobs = new DbcConcurrentWork<string, string?>(1);
            Assert.Null(await jobs.RunAsync("source", _ => Task.FromResult<string?>(sequence.Value), CancellationToken.None));
            Assert.Equal("old execution", sequence.Value);
        }
        finally { sequence.Value = null; }
    }

    [Fact]
    public async Task Dropping_one_waiter_does_not_cancel_the_surviving_read()
    {
        var jobs = new DbcConcurrentWork<string, string>(1);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var stop = new CancellationTokenSource();
        int reads = 0;
        async Task<string> Read(CancellationToken token)
        {
            Interlocked.Increment(ref reads);
            start.SetResult();
            await finish.Task.WaitAsync(token);
            return "result";
        }
        var first = jobs.RunAsync("file", Read, stop.Token);
        await start.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = jobs.RunAsync("file", Read, CancellationToken.None);
        stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        finish.SetResult();
        Assert.Equal("result", await second.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(1, reads);
    }

    [Fact]
    public async Task Outstanding_limit_includes_cancelled_native_reads_until_they_return()
    {
        var jobs = new DbcConcurrentWork<string, int>(1, maxOutstanding: 1);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var caller = new CancellationTokenSource();
        try
        {
            var old = jobs.RunAsync("file", async _ => { start.SetResult(); await release.Task; return 1; }, caller.Token);
            await start.Task.WaitAsync(TimeSpan.FromSeconds(5));
            caller.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => old);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                jobs.RunAsync("replacement", _ => Task.FromResult(2), CancellationToken.None));
            Assert.Contains("尚未退出", ex.Message);
        }
        finally { release.TrySetResult(); }
    }

    [Fact]
    public async Task Abandoned_worker_cannot_remove_a_new_generation_of_the_same_key()
    {
        var jobs = new DbcConcurrentWork<string, string>(2);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finishOld = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var enteredNew = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finishNew = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var stop = new CancellationTokenSource();
        var old = jobs.RunAsync("source", async token =>
        {
            using var registration = token.Register(() => cancelled.TrySetResult());
            entered.SetResult(); await finishOld.Task;
            throw new IOException("late read failure");
        }, stop.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => old);
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var fresh = jobs.RunAsync("source", async token =>
        {
            enteredNew.SetResult(); await finishNew.Task.WaitAsync(token); return "fresh";
        }, CancellationToken.None);
        await enteredNew.Task.WaitAsync(TimeSpan.FromSeconds(5));
        finishOld.SetResult();
        // With both slots occupied this marker can enter only after old work releases its slot.
        await jobs.RunAsync("marker", _ => Task.FromResult("done"), CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        var join = jobs.RunAsync("source", _ => throw new InvalidOperationException("duplicate read"), CancellationToken.None);
        finishNew.SetResult();
        Assert.Equal(new[] { "fresh", "fresh" }, await Task.WhenAll(fresh, join));
    }

    [Fact]
    public async Task A_fault_is_observed_and_can_be_retried()
    {
        int attempts = 0;
        var store = new DbcCatalogStore((text, ct) => ++attempts == 1
            ? throw new FormatException("模拟解析失败") : DbcDatabase.ParseCatalog(text, ct));
        await Assert.ThrowsAsync<FormatException>(() => store.LoadAsync(Text, CancellationToken.None));
        Assert.NotEmpty((await store.LoadAsync(Text, CancellationToken.None)).Database.Messages);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task Same_path_same_size_and_timestamp_still_reloads_changed_contents()
    {
        string path = Path.Combine(Path.GetTempPath(), $"dbc-sharing-{Guid.NewGuid():N}.dbc");
        try
        {
            await File.WriteAllTextAsync(path, Text);
            DateTime stamp = File.GetLastWriteTimeUtc(path);
            var original = await DbcSharedCatalog.LoadAsync(await DbcDocumentIO.ReadAsync(path, "Auto"));
            var embedded = await DbcSharedCatalog.LoadAsync(new() { Text = Text });
            Assert.Same(original.Database, embedded.Database);
            await File.WriteAllTextAsync(path, Text.Replace("Good", "More"));
            File.SetLastWriteTimeUtc(path, stamp);
            var changed = await DbcSharedCatalog.LoadAsync(await DbcDocumentIO.ReadAsync(path, "Auto"));
            Assert.NotEqual(original.Fingerprint, changed.Fingerprint);
            Assert.Equal("Good", original.Database.Find(0x123, false).Name);
            Assert.Equal("More", changed.Database.Find(0x123, false).Name);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Incremental_fingerprint_handles_surrogate_pairs_across_chunks()
    {
        string text = new string('x', 4095) + "🚗中文";
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))),
            DbcDocumentIO.Fingerprint(new() { Text = text }));
    }
}
