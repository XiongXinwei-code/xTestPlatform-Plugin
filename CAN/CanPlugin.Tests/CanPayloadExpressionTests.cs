using System.Collections.Concurrent;
using System.Dynamic;
using System.Reflection;
using System.Security.Cryptography;
using CAN.Helpers;
using CAN.Models;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace CAN.Tests;

[Collection("CAN blocked I/O")]
public sealed class CanPayloadExpressionTests
{
    private static CanPayloadProtectionSetting Setting() => new()
    {
        CrcEnabled = true, CrcDataIdEnabled = true, CrcDataIdMode = CanCrcDataIdMode.Profile2,
        CrcDataIdTableUseExpression = true, CrcDataIdTableExpression = "Locals.DataIds",
        CrcDataIdTable = Enumerable.Repeat("invalid fallback", 16).ToArray()
    };

    private static (IExecutionContext Context, PayloadExpressionContext State) Context(object? value)
    {
        var context = DispatchProxy.Create<IExecutionContext, PayloadExpressionContext>();
        var state = (PayloadExpressionContext)(object)context;
        state.Variables["Locals.DataIds"] = value;
        return (context, state);
    }

    [Fact]
    public void Static_layout_copy_uses_placeholder_only_for_active_expression_and_preserves_original()
    {
        Assert.Null(CanPayloadProtectionExpressions.ForLayoutValidation(null));
        var original = Setting(); original.CrcStartByte = 3;
        var layout = CanPayloadProtectionExpressions.ForLayoutValidation(original)!;
        Assert.False(layout.CrcDataIdTableUseExpression); Assert.Equal(3, layout.CrcStartByte);
        Assert.All(layout.CrcDataIdTable, entry => Assert.Equal("00", entry));
        Assert.True(original.CrcDataIdTableUseExpression);
        Assert.All(original.CrcDataIdTable, entry => Assert.Equal("invalid fallback", entry));
        original.CrcEnabled = false;
        var dormant = CanPayloadProtectionExpressions.ForLayoutValidation(original)!;
        Assert.True(dormant.CrcDataIdTableUseExpression);
        Assert.All(dormant.CrcDataIdTable, entry => Assert.Equal("invalid fallback", entry));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Inline_or_disabled_modes_never_evaluate_dormant_expression(int disabled)
    {
        var original = Setting();
        switch (disabled)
        {
            case 0: original.CrcDataIdTableUseExpression = false; break;
            case 1: original.CrcEnabled = false; break;
            case 2: original.CrcDataIdEnabled = false; break;
            case 3: original.CrcDataIdMode = CanCrcDataIdMode.Profile1Alt; break;
        }
        var (context, state) = Context(new byte[16]);
        state.BeforeRead = () => throw new InvalidOperationException("Dormant expression was evaluated");
        var logs = new List<string>();
        var actual = await CanPayloadProtectionExpressions.ResolveAsync(original, context, default, () => { }, logs.Add);
        Assert.Equal(0, state.ReadCount); Assert.Empty(logs); Assert.NotSame(original, actual);
        Assert.NotSame(original.CrcDataIdTable, actual.CrcDataIdTable); Assert.False(CanPayloadProtectionExpressions.UsesTableExpression(actual));
    }

    [Fact]
    public async Task Empty_active_expression_is_rejected_without_inline_fallback_or_evaluation()
    {
        var original = Setting(); original.CrcDataIdTableExpression = " ";
        original.CrcDataIdTable = Enumerable.Repeat("AB", 16).ToArray();
        var (context, state) = Context(new byte[16]);
        await Assert.ThrowsAsync<ArgumentException>(() => CanPayloadProtectionExpressions.ResolveAsync(original, context, default, () => { }));
        Assert.Equal(0, state.ReadCount);
    }

    public static IEnumerable<object?[]> InvalidValues()
    {
        yield return [null];
        yield return [new byte[15]];
        yield return [new byte[17]];
        yield return [new int[16]];
        yield return [Enumerable.Repeat(256, 16).ToArray()];
        yield return ["01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E 0F 10"];
        yield return [Enumerable.Range(0, 16).Select(value => (object)(byte)value).ToArray()];
    }

    [Theory]
    [MemberData(nameof(InvalidValues))]
    public async Task Active_expression_requires_exact_byte_array_and_length(object? value)
    {
        var original = Setting(); original.CrcDataIdTable = Enumerable.Repeat("AB", 16).ToArray();
        var (context, _) = Context(value); var logs = new List<string>();
        var error = await Assert.ThrowsAsync<ArgumentException>(() =>
            CanPayloadProtectionExpressions.ResolveAsync(original, context, default, () => { }, logs.Add));
        Assert.Contains("byte[]", error.Message); Assert.Empty(logs);
        Assert.True(original.CrcDataIdTableUseExpression);
    }

    [Fact]
    public async Task Valid_result_is_copied_logged_and_immune_to_later_variable_and_setting_edits()
    {
        var bytes = Enumerable.Range(1, 16).Select(value => (byte)value).ToArray();
        string expectedHex = Convert.ToHexString(bytes), expectedHash = Convert.ToHexString(SHA256.HashData(bytes));
        var (context, state) = Context(bytes); var original = Setting(); var logs = new List<string>();
        var actual = await CanPayloadProtectionExpressions.ResolveAsync(original, context, default, () => { }, logs.Add);
        Assert.False(actual.CrcDataIdTableUseExpression); Assert.True(original.CrcDataIdTableUseExpression);
        Assert.Equal(expectedHex, string.Concat(actual.CrcDataIdTable));
        Array.Fill(bytes, (byte)0xFF); original.CrcDataIdTableExpression = "Locals.Other";
        Array.Fill(original.CrcDataIdTable, "EE");
        Assert.Equal(expectedHex, string.Concat(actual.CrcDataIdTable));
        string log = Assert.Single(logs);
        Assert.Contains(string.Join(" ", actual.CrcDataIdTable), log); Assert.Contains(expectedHash, log);
        Assert.DoesNotContain("Locals.Other", log); Assert.Single(state.Variables);
    }

    [Fact]
    public async Task Already_cancelled_resolution_never_starts_evaluator_or_publishes()
    {
        var (context, state) = Context(new byte[16]); using var cancel = new CancellationTokenSource(); cancel.Cancel();
        var logs = new List<string>();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CanPayloadProtectionExpressions.ResolveAsync(Setting(), context, cancel.Token, () => { }, logs.Add));
        Assert.Equal(0, state.ReadCount); Assert.Empty(logs);
    }

    [Fact]
    public async Task Tracker_retains_actual_evaluator_after_cancelled_wait_until_it_really_exits()
    {
        using var release = new ManualResetEventSlim(); using var cancel = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var (context, state) = Context(new byte[16]); var logs = new ConcurrentQueue<string>();
        state.BeforeRead = () => { entered.TrySetResult(); release.Wait(); };
        Task? actual = null;
        var run = CanPayloadProtectionExpressions.ResolveAsync(Setting(), context, cancel.Token, () => { }, logs.Enqueue, task => actual = task);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); Assert.NotNull(actual);
            cancel.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
            Assert.False(actual!.IsCompleted); Assert.Empty(logs);
            release.Set(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => actual);
            Assert.True(actual.IsCompleted); Assert.Empty(logs);
        }
        finally { release.Set(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancelled_or_retired_wait_cannot_publish_late_evaluator_result(bool retire)
    {
        using var release = new ManualResetEventSlim(); using var cancel = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readReturned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var (context, state) = Context(new byte[16]); var logs = new ConcurrentQueue<string>();
        int retired = 0;
        state.BeforeRead = () => { entered.TrySetResult(); release.Wait(); };
        state.AfterRead = () => readReturned.TrySetResult();
        void Check() { if (Volatile.Read(ref retired) != 0) throw new InvalidOperationException("运行代次已经替换"); }
        var run = CanPayloadProtectionExpressions.ResolveAsync(Setting(), context, cancel.Token, Check, logs.Enqueue);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            if (retire) { Volatile.Write(ref retired, 1); release.Set(); await Assert.ThrowsAsync<InvalidOperationException>(() => run); }
            else { cancel.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run); release.Set(); }
            await readReturned.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Task.Delay(30);
            Assert.Empty(logs); Assert.Single(state.Variables);
        }
        finally { release.Set(); }
    }
}

public class PayloadExpressionContext : DbcTestContext
{
    private readonly Dictionary<string, PayloadExpressionScope> _scopes = new(StringComparer.Ordinal);
    public Action? BeforeRead { get; set; }
    public Action? AfterRead { get; set; }
    public int ReadCount;
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        string? scopeName = method!.Name switch
        {
            "get_Locals" => "Locals", "get_Parameters" => "Parameters",
            "get_FileGlobals" => "FileGlobals", "get_ProjectGlobals" => "ProjectGlobals",
            "get_RunState" => "RunState", _ => null
        };
        if (scopeName == null) return base.Invoke(method, args);
        if (!_scopes.TryGetValue(scopeName, out var scope)) _scopes[scopeName] = scope = new(this, scopeName);
        return scope;
    }

    internal object? Read(string path)
    {
        // ScriptGlobals also reads RunState.Steps through GetVariable while
        // building globals. Synchronize only the expression's actual table read.
        bool observed = path == "Locals.DataIds";
        if (observed) { Interlocked.Increment(ref ReadCount); BeforeRead?.Invoke(); }
        var value = Variables.GetValueOrDefault(path);
        if (observed) AfterRead?.Invoke();
        return value;
    }
}

public sealed class PayloadExpressionScope(PayloadExpressionContext owner, string scopeName) : DynamicObject, IVariableScope
{
    private string Prefix => scopeName + ".";
    public override bool TryGetMember(GetMemberBinder binder, out object? result)
    {
        string path = Prefix + binder.Name;
        result = owner.Read(path);
        return owner.Variables.ContainsKey(path);
    }
    public IEnumerable<string> GetAllNames() => owner.Variables.Keys.Where(key => key.StartsWith(Prefix, StringComparison.Ordinal)).Select(key => key[Prefix.Length..]).ToArray();
    public bool Contains(string name) => owner.Variables.ContainsKey(Prefix + name);
    public void Clear() { foreach (string name in GetAllNames()) owner.Variables.Remove(Prefix + name); }
    public Variables? GetVariableDefinition(string name) => null;
    public IEnumerable<Variables> GetAllDefinitions() => [];
    public event EventHandler<ScopeVariableChangedEventArgs>? VariableChanged { add { } remove { } }
}
