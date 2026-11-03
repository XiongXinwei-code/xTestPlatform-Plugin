using NiGrpc.UI.Editors;
using NiGrpc.UI.ViewModels;
using NiGrpc.UI.Views;
using xTestPlatform.Core.SequenceModels;
using xTestPlatform.Core.Plugins.Contracts;
using Xunit;

namespace NiGrpc.Tests;

public sealed class EditorTests
{
    [Fact]
    public void Every_step_has_its_own_round_trip_setting_and_unique_icon()
    {
        var plugins = typeof(NiGrpcConnectPlugin).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(IStepPlugin).IsAssignableFrom(t))
            .Select(t => (IStepPlugin)Activator.CreateInstance(t)!).ToArray();
        Assert.Equal(36, plugins.Length);
        Assert.Equal(36, plugins.Select(p => p.IconPath).Distinct().Count());
        var settingTypes = new HashSet<Type>();
        foreach (var plugin in plugins)
        {
            var serializer = plugin.CreateSerializer();
            var original = (NiGrpcSetting)serializer.CreateDefault();
            original.ConnectionName = "\"专用连接\"";
            if (original is NiGrpcSessionSetting session) session.SessionName = "\"会话别名\"";
            var copy = (NiGrpcSetting)serializer.Deserialize(serializer.Serialize(original), 1);
            Assert.Equal(original.GetType(), copy.GetType());
            Assert.True(settingTypes.Add(copy.GetType()));
            Assert.Equal(original.ConnectionName, copy.ConnectionName);
            if (original is NiGrpcSessionSetting expected) Assert.Equal(expected.SessionName, ((NiGrpcSessionSetting)copy).SessionName);
            Assert.Contains("## 相关插件", plugin.Description);
        }
    }
    [Fact]
    public void Editors_match_steps_and_create_wpf_views() => UiThread.Run(() =>
    {
        NiGrpcEditorBase[] editors = typeof(NiGrpcEditorBase).Assembly.GetTypes().Where(t => !t.IsAbstract && typeof(NiGrpcEditorBase).IsAssignableFrom(t)).Select(t => (NiGrpcEditorBase)Activator.CreateInstance(t)!).ToArray();
        Assert.Equal(36, editors.Select(x => x.StepTypeId).Distinct().Count());
        foreach (var editor in editors)
        {
            var view = Assert.IsType<NiGrpcEditorView>(editor.CreateEditor(new Step(), null));
            Assert.NotNull(view.ViewModel);
        }
    });

    [Fact]
    public void Pending_edit_is_flushed_to_original_step_before_refresh() => UiThread.Run(() =>
    {
        var serializer = new NiGrpcConnectPlugin().CreateSerializer();
        var first = new Step(); var second = new Step();
        first.StepSetting.SettingVersion = second.StepSetting.SettingVersion = 1;
        first.StepSetting.Setting = serializer.Serialize(new NiGrpcConnectSetting());
        second.StepSetting.Setting = serializer.Serialize(new NiGrpcConnectSetting());
        var vm = new NiGrpcViewModel();
        vm.Attach(first, serializer); vm.Endpoint = "\"http://127.0.0.1:31763\"";
        vm.Attach(second, serializer);
        Assert.Equal("\"http://127.0.0.1:31763\"", ((NiGrpcConnectSetting)serializer.Deserialize(first.StepSetting.Setting, 1)).Endpoint);
        Assert.Equal("\"http://localhost:31763\"", vm.Endpoint);
    });
}
