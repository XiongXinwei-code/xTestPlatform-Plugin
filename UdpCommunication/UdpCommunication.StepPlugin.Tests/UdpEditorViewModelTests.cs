using UdpCommunication.StepPlugin;
using UdpCommunication.StepPlugin.Models;
using UdpCommunication.StepPlugin.UI;
using UdpCommunication.StepPlugin.UI.ViewModels;
using UdpCommunication.StepPlugin.UI.Views;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace UdpCommunication.StepPlugin.Tests;

public sealed class UdpEditorViewModelTests
{
    [Fact]
    public async Task SendEditor_Save_UpdatesOnlyPluginSetting()
    {
        var step = new Step();
        step.PropertiesSetting.General.StepDescription = "框架管理的原始描述";
        var viewModel = new UdpEditorViewModel(
            new UdpSendPlugin().CreateSerializer(),
            _ => "插件不应写入描述",
            false);
        viewModel.AttachStep(step);

        viewModel.RequestData = "PING";
        await WaitUntilAsync(() => step.StepSetting.Setting.Length > 0);

        Assert.NotEmpty(step.StepSetting.Setting);
        Assert.Equal("框架管理的原始描述", step.PropertiesSetting.General.StepDescription);
    }

    [Fact]
    public void SendEditor_CommitPendingChanges_WritesCurrentSettingImmediately()
    {
        var plugin = new UdpSendPlugin();
        var step = new Step();
        var viewModel = new UdpEditorViewModel(plugin.CreateSerializer(), _ => string.Empty, false);
        viewModel.AttachStep(step);
        viewModel.RequestData = "ENTER-COMMITTED";

        viewModel.CommitPendingChanges();

        var saved = (UdpSendSetting)plugin.CreateSerializer().Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
        Assert.Equal("ENTER-COMMITTED", saved.RequestData);
    }

    [Fact]
    public void SendEditor_ThrowingDescriptionGenerator_UsesFixedHostCommandLabel()
    {
        var viewModel = new UdpEditorViewModel(new UdpSendPlugin().CreateSerializer(), _ => throw new InvalidOperationException(), false);
        string? label = null;
        viewModel.ExecuteCommand = (description, action) => { label = description; action(); };
        viewModel.AttachStep(new Step());
        viewModel.RequestData = "PING";

        viewModel.CommitPendingChanges();

        Assert.Equal("更新 UDP 步骤配置", label);
    }

    [Fact]
    public void SendEditor_ThrowingHostCommand_WritesSerializedSettingDirectly()
    {
        var plugin = new UdpSendPlugin();
        var step = new Step();
        var viewModel = new UdpEditorViewModel(plugin.CreateSerializer(), _ => "UDP", false)
        {
            ExecuteCommand = (_, _) => throw new InvalidOperationException("host failed")
        };
        viewModel.AttachStep(step);
        viewModel.RequestData = "FALLBACK";

        viewModel.CommitPendingChanges();

        var saved = (UdpSendSetting)plugin.CreateSerializer().Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
        Assert.Equal("FALLBACK", saved.RequestData);
    }

    [Fact]
    public void SendEditor_ThrowingHostCommand_PersistsAlreadySerializedBytesWithoutReserializing()
    {
        var serializer = new DistinguishingSerializer();
        var step = new Step();
        var viewModel = new UdpEditorViewModel(serializer, _ => "UDP", false)
        {
            ExecuteCommand = (_, _) => throw new InvalidOperationException("host failed")
        };
        viewModel.AttachStep(step);
        viewModel.RequestData = "FALLBACK";

        viewModel.CommitPendingChanges();

        Assert.Equal(1, serializer.SerializeCallCount);
        Assert.Equal(new byte[] { 0xA1 }, step.StepSetting.Setting);
    }

    [Fact]
    public void SendEditor_CommitPendingChanges_UsesInjectedHostCommand()
    {
        var plugin = new UdpSendPlugin();
        var step = new Step();
        var viewModel = new UdpEditorViewModel(plugin.CreateSerializer(), _ => string.Empty, false);
        Action? hostAction = null;
        string? commandDescription = null;
        viewModel.ExecuteCommand = (description, action) =>
        {
            commandDescription = description;
            hostAction = action;
        };
        viewModel.AttachStep(step);
        viewModel.RequestData = "REFRESH-DESCRIPTION";

        viewModel.CommitPendingChanges();

        Assert.NotNull(hostAction);
        Assert.False(string.IsNullOrWhiteSpace(commandDescription));
        Assert.Empty(step.StepSetting.Setting);

        hostAction();
        var saved = (UdpSendSetting)plugin.CreateSerializer().Deserialize(
            step.StepSetting.Setting,
            step.StepSetting.SettingVersion);
        Assert.Equal("REFRESH-DESCRIPTION", saved.RequestData);
    }

    [Fact]
    public async Task SendEditor_DebouncedSave_UsesInjectedHostCommandAndGeneratedDescription()
    {
        var plugin = new UdpSendPlugin();
        var step = new Step();
        var viewModel = new UdpEditorViewModel(
            plugin.CreateSerializer(),
            _ => "UDP summary",
            false);
        Action? hostAction = null;
        string? commandDescription = null;
        viewModel.ExecuteCommand = (description, action) =>
        {
            commandDescription = description;
            hostAction = action;
        };
        viewModel.AttachStep(step);

        viewModel.RequestData = "DEBOUNCED";

        await WaitUntilAsync(() => hostAction is not null);

        Assert.Contains("UDP summary", commandDescription, StringComparison.Ordinal);
        Assert.Empty(step.StepSetting.Setting);

        hostAction!();
        var saved = (UdpSendSetting)plugin.CreateSerializer().Deserialize(
            step.StepSetting.Setting,
            step.StepSetting.SettingVersion);
        Assert.Equal("DEBOUNCED", saved.RequestData);
    }

    [Fact]
    public void EditorView_DeclaresFrameworkInjectionProperties()
    {
        var viewType = typeof(UdpEditorView);

        Assert.Equal(
            typeof(Action<string, Action>),
            viewType.GetProperty(nameof(UdpEditorView.ExecuteCommand))?.PropertyType);
        Assert.Equal(
            typeof(SequenceFile),
            viewType.GetProperty(nameof(UdpEditorView.SequenceFile))?.PropertyType);
        Assert.Equal(
            typeof(EditPosition),
            viewType.GetProperty(nameof(UdpEditorView.EditPosition))?.PropertyType);
    }

    [Fact]
    public void SendEditor_SwitchStep_CommitsPendingChangesToPreviousStep()
    {
        var plugin = new UdpSendPlugin();
        var firstStep = new Step();
        var secondStep = new Step();
        var viewModel = new UdpEditorViewModel(plugin.CreateSerializer(), _ => string.Empty, false);
        viewModel.AttachStep(firstStep);
        viewModel.RequestData = "SAVE-BEFORE-SWITCH";

        viewModel.AttachStep(secondStep);

        var saved = (UdpSendSetting)plugin.CreateSerializer().Deserialize(firstStep.StepSetting.Setting, firstStep.StepSetting.SettingVersion);
        Assert.Equal("SAVE-BEFORE-SWITCH", saved.RequestData);
    }

    [Fact]
    public void SendEditor_ReceiveProperties_AreSafeForCollapsedBindings()
    {
        var step = new Step();
        var viewModel = new UdpEditorViewModel(
            new UdpSendPlugin().CreateSerializer(),
            _ => string.Empty,
            false);
        viewModel.AttachStep(step);

        Assert.Equal(0, viewModel.ReceiveTimeoutMs);
    }

    [Fact]
    public async Task EditorValidation_CorruptSetting_ReturnsErrorInsteadOfThrowing()
    {
        var editor = new UdpSendEditorPlugin();
        var result = await editor.ValidateWithContextAsync([0xC1], null!, TestExecutionContextFactory.Create(new Step()));

        var error = Assert.Single(result);
        Assert.Equal("UDP_000", error.Code);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var timeoutAt = DateTime.UtcNow.AddSeconds(2);
        while (!condition())
        {
            if (DateTime.UtcNow >= timeoutAt)
            {
                throw new TimeoutException("等待编辑器保存配置超时");
            }

            await Task.Delay(20);
        }
    }

    private sealed class DistinguishingSerializer : IStepSettingSerializer
    {
        public int SerializeCallCount { get; private set; }
        public int SettingVersion => 1;

        public byte[] Serialize(object setting) => ++SerializeCallCount == 1 ? [0xA1] : [0xB2];

        public object Deserialize(byte[] data, int dataVersion) => new UdpSendSetting();

        public object CreateDefault() => new UdpSendSetting();
    }
}
