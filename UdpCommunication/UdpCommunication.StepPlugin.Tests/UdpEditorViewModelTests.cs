using StepEditor.Abstractions;
using UdpCommunication;
using UdpCommunication.Models;
using UdpCommunication.UI;
using UdpCommunication.UI.Editors;
using UdpCommunication.UI.ViewModels;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Services.ExpressionEngine;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace UdpCommunication.Tests;

public sealed class UdpEditorViewModelTests : IDisposable
{
    private readonly IDisposable _evaluatorScope = TestExecutionContextFactory.Use(new MockExpressionEvaluator());

    public void Dispose() => _evaluatorScope.Dispose();

    [Fact]
    public async Task SendEditor_Save_UpdatesOnlyPluginSetting()
    {
        var step = new Step();
        step.PropertiesSetting.General.StepDescription = "Framework managed description";
        var viewModel = new UdpSendViewModel();
        viewModel.AttachSerializer(new UdpSendPlugin().CreateSerializer());
        viewModel.AttachStep(step);

        viewModel.RequestData = "PING";
        await WaitUntilAsync(() => step.StepSetting.Setting.Length > 0);

        Assert.NotEmpty(step.StepSetting.Setting);
        Assert.Equal("Framework managed description", step.PropertiesSetting.General.StepDescription);
    }

    [Fact]
    public void SendEditor_SettingChange_PersistsSerializedBytesToStep()
    {
        var plugin = new UdpSendPlugin();
        var step = new Step();
        var viewModel = new UdpSendViewModel();
        viewModel.AttachSerializer(plugin.CreateSerializer());
        viewModel.AttachStep(step);
        viewModel.RequestData = "ENTER-COMMITTED";
        viewModel.FlushPendingChanges();

        var saved = (UdpSendSetting)plugin.CreateSerializer().Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
        Assert.Equal("ENTER-COMMITTED", saved.RequestData);
    }

    [Fact]
    public async Task EditorValidation_CorruptSetting_ReturnsErrorInsteadOfThrowing()
    {
        var editor = new UdpSendEditorPlugin();
        var (context, _) = TestExecutionContextFactory.CreateWithProxy(new UdpSendSetting(), new Step());
        var validationContext = new StepEditorValidationContext
        {
            Setting = [0xC1],
            Evaluator = ExpressionEvaluatorFactory.Default,
            ExecutionContext = context,
            SequenceFile = null!,
            Block = null!,
            CurrentStep = null!
        };

        var result = await editor.ValidateWithContextAsync(validationContext);

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
                throw new TimeoutException("Wait for editor to save setting timed out");
            }

            await Task.Delay(20);
        }
    }
}
