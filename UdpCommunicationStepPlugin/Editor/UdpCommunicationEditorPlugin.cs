using System.Windows;
using StepEditor.Abstractions;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.SequenceModels;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.Services.ExpressionEngine;
using UdpCommunicationStepPlugin.Setting;
using UdpCommunicationStepPlugin.View;

namespace UdpCommunicationStepPlugin.Editor;

public sealed class UdpCommunicationEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "Example.Network.UdpCommunication";
    public string IconPath => string.Empty;

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new UdpCommunicationEditorView();
        view.ViewModel.AttachSerializer(new UdpCommunicationPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken cancellationToken = default)
    {
        var serializer = new UdpCommunicationPlugin().CreateSerializer();
        var value = (UdpCommunicationSetting)serializer.Deserialize(setting, 1);
        IReadOnlyList<StepSettingError> errors = UdpSettingValidator.Validate(value)
            .Select(issue => StepSettingError.Error(issue.Code, issue.Message))
            .ToList();
        return Task.FromResult(errors);
    }
}
