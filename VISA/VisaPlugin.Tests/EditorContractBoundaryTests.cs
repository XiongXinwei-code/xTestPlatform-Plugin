using System.Collections;
using System.Reflection;
using System.Windows;
using Ivi.Visa;
using VISA.Helpers;
using VISA.Models;
using VISA.UI;
using VISA.UI.Views;
using xTestPlatform.Core.Models;
using xTestPlatform.Core.SequenceModels;
using StepEditor.Abstractions;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Services.ExpressionEngine;
using Xunit;

namespace VISA.Tests;

public sealed class EditorContractBoundaryTests
{
    [Theory]
    [InlineData(-1, 10000)]
    [InlineData(5000, -1)]
    public async Task Supported_infinite_timeout_options_are_not_rejected_by_the_editor(int opening, int io)
    {
        var plugin = new VisaOpenPlugin(); var (context, _) = PublicationContextProxy.Create(plugin, new VisaOpenSetting { OpenTimeoutMs = opening, IoTimeoutMs = io });
        var step = context.CurrentStep!.Step;
        var errors = await new VisaOpenEditorPlugin().ValidateWithContextAsync(new StepEditorValidationContext { Setting = step.StepSetting.Setting,
            ExecutionContext = context, Evaluator = ExpressionEvaluatorFactory.Default, CurrentStep = step, SequenceFile = new SequenceFile(), Block = [step] });
        Assert.DoesNotContain(errors, error => error.Code is "VISA_003" or "VISA_004");
    }

    [Fact]
    public void Direct_batch_collection_clear_removes_persisted_model_rows() => UiThread.Run(() =>
    {
        var serializer = new VisaBatchWritePlugin().CreateSerializer();
        var step = new Step(); step.StepSetting.SettingVersion = 1;
        step.StepSetting.Setting = serializer.Serialize(new VisaBatchWriteSetting { Items = [new VisaBatchWriteItem { Command = "\"*CLS\"" }] });
        var page = (VisaBatchWriteEditorView)new VisaBatchWriteEditorPlugin().CreateEditor(step, new SequenceFile());
        page.ViewModel.Items.Clear();
        page.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        Assert.Empty(((VisaBatchWriteSetting)serializer.Deserialize(step.StepSetting.Setting, 1)).Items);
    });

    [Fact]
    public void Actual_batch_page_binds_notifies_and_serializes_original_model_items() => UiThread.Run(() =>
    {
        var serializer = new VisaBatchWritePlugin().CreateSerializer();
        var step = new Step(); step.StepSetting.SettingVersion = 1; step.StepSetting.Setting = serializer.Serialize(new VisaBatchWriteSetting());
        var page = (VisaBatchWriteEditorView)new VisaBatchWriteEditorPlugin().CreateEditor(step, new SequenceFile());
        var row = new VisaBatchWriteItem { Command = "\"*CLS\"", DelayMs = 17 };
        var collection = Assert.IsAssignableFrom<IList>(page.ViewModel.Items);
        collection.Add(row); row.Command = "\"OUTP OFF\"";
        page.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        var saved = (VisaBatchWriteSetting)serializer.Deserialize(step.StepSetting.Setting, 1);
        Assert.Equal("\"OUTP OFF\"", Assert.Single(saved.Items).Command);
        Assert.Equal(17, saved.Items[0].DelayMs);
        page.RefreshFromStep(step);
        Assert.IsType<VisaBatchWriteItem>(Assert.Single(page.ViewModel.Items.Cast<object>()));
    });

    [Fact]
    public async Task Negative_wait_opc_timeout_is_an_error_without_native_submission()
    {
        var native = DispatchProxy.Create<IMessageBasedSession, CompletionBoundaryTests.Session>();
        var owner = new VisaSessionResource(native, 2000, "\n");
        var plugin = new VisaWaitOpcPlugin();
        var (context, state) = PublicationContextProxy.Create(plugin, new VisaWaitOpcSetting { ConnectionName = "\"C\"", TimeoutMs = -2 });
        state.Store.Values[VisaHelper.GetSessionKey("C")] = owner;
        try
        {
            var result = await plugin.CreateExecutor().ExecuteAsync(context).WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Equal(TestStatus.Error, result.StepResult.Status);
            Assert.Equal(0, ((CompletionBoundaryTests.Session)(object)native).Writes);
        }
        finally { await owner.CloseAsync(2000, default); }
    }
}
