using System.Windows;
using SerialPort.Models;
using SerialPort.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace SerialPort.UI;

public sealed class SerialPortCloseEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.SerialPortClose";
    public string IconPath => "pack://application:,,,/SerialPort.StepPlugin.UI;component/Resources/Icons/serialport.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new SerialPortCloseEditorView();
        view.ViewModel.AttachSerializer(new SerialPortClosePlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        byte[] setting,
        IExpressionEvaluator evaluator,
        IExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<StepSettingError>();
        var serializer = new SerialPortClosePlugin().CreateSerializer();
        var s = setting is { Length: > 0 }
            ? (SerialPortCloseSetting)serializer.Deserialize(setting, 1)
            : new SerialPortCloseSetting();

        if (string.IsNullOrWhiteSpace(s.PortName))
            errors.Add(StepSettingError.Error("SP_010", "PortName 不能为空"));

        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
