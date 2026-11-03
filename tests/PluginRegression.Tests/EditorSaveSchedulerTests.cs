using System.Reflection;
using Xunit;

namespace PluginRegression.Tests;

public sealed class EditorSaveSchedulerTests
{
    [StaFact]
    public void Host_callback_is_captured_at_edit_and_retried_even_after_bytes_were_applied()
    {
        var type = typeof(Http.UI.ViewModels.HttpClientCreateViewModel).Assembly.GetType("PluginCommon.EditorSaveScheduler")!;
        var scheduler = Activator.CreateInstance(type)!; var queue = type.GetMethod("Queue")!; var flush = type.GetMethod("Flush")!;
        int original = 0, replacement = 0, writes = 0;
        Action<string, Action> command = (_, apply) => { apply(); if (++original == 1) throw new InvalidOperationException("description refresh failed"); };
        type.GetProperty("ExecuteCommand")!.SetValue(scheduler, command);
        queue.Invoke(scheduler, [(Action)(() => writes++)]);
        type.GetProperty("ExecuteCommand")!.SetValue(scheduler, (Action<string, Action>)((_, _) => replacement++));
        Assert.Throws<TargetInvocationException>(() => flush.Invoke(scheduler, null));
        flush.Invoke(scheduler, null);
        Assert.Equal(2, original); Assert.Equal(2, writes); Assert.Equal(0, replacement);
        Assert.Null(type.GetProperty("LastError")!.GetValue(scheduler));
    }

    [StaFact]
    public void Http_step_switch_survives_host_refresh_and_preserves_the_new_destination()
    {
        var plugin = new Http.HttpClientCreatePlugin(); var serializer = plugin.CreateSerializer();
        var first = new xTestPlatform.Core.SequenceModels.Step(); var second = new xTestPlatform.Core.SequenceModels.Step();
        first.StepSetting.Setting = serializer.Serialize(serializer.CreateDefault()); first.StepSetting.SettingVersion = serializer.SettingVersion;
        var other = (Http.Models.HttpClientCreateSetting)serializer.CreateDefault(); other.ClientName = "\"SECOND\"";
        second.StepSetting.Setting = serializer.Serialize(other); second.StepSetting.SettingVersion = serializer.SettingVersion;
        var model = new Http.UI.ViewModels.HttpClientCreateViewModel(); model.AttachSerializer(serializer); model.AttachStep(first);
        model.ExecuteCommand = (_, apply) => { apply(); model.AttachStep(first); };
        model.ClientName = "\"FIRST-EDITED\""; model.AttachStep(second);
        Assert.Equal("\"SECOND\"", model.ClientName);
        Assert.Equal("\"FIRST-EDITED\"", ((Http.Models.HttpClientCreateSetting)serializer.Deserialize(first.StepSetting.Setting, serializer.SettingVersion)).ClientName);
    }

    [StaFact]
    public void Flush_is_reentrant_safe_and_retains_failed_save_for_retry()
    {
        var type = typeof(Http.UI.ViewModels.HttpClientCreateViewModel).Assembly.GetType("PluginCommon.EditorSaveScheduler")!;
        var scheduler = Activator.CreateInstance(type)!; var queue = type.GetMethod("Queue")!; var flush = type.GetMethod("Flush")!;
        int saves = 0;
        queue.Invoke(scheduler, [(Action)(() => { if (++saves == 1) flush.Invoke(scheduler, null); })]);
        flush.Invoke(scheduler, null); Assert.Equal(1, saves);
        bool fail = true;
        queue.Invoke(scheduler, [(Action)(() => { if (fail) throw new InvalidOperationException("save failure"); saves++; })]);
        var error = Assert.Throws<TargetInvocationException>(() => flush.Invoke(scheduler, null));
        Assert.IsType<InvalidOperationException>(error.InnerException);
        Assert.NotNull(type.GetProperty("LastError")!.GetValue(scheduler));
        fail = false; flush.Invoke(scheduler, null); Assert.Equal(2, saves);
        Assert.Null(type.GetProperty("LastError")!.GetValue(scheduler));
    }
}
