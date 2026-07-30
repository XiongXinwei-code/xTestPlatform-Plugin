using System.Windows;
using SerialPort.Models;
using SerialPort.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace SerialPort.UI;

public sealed class SerialPortReadEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.SerialPortRead";
    public string IconPath => "pack://application:,,,/SerialPort.StepPlugin.UI;component/Resources/Icons/serialport.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new SerialPortReadEditorView();
        view.ViewModel.AttachSerializer(new SerialPortReadPlugin().CreateSerializer());
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
        var serializer = new SerialPortReadPlugin().CreateSerializer();
        var s = setting is { Length: > 0 }
            ? (SerialPortReadSetting)serializer.Deserialize(setting, 1)
            : new SerialPortReadSetting();

        if (string.IsNullOrWhiteSpace(s.PortName))
            errors.Add(StepSettingError.Error("SP_030", "PortName 不能为空"));

        if (s.ReadTimeoutMs <= 0)
            errors.Add(StepSettingError.Error("SP_031", "ReadTimeout 必须大于 0"));

        if (s.ReadBytes < 0)
            errors.Add(StepSettingError.Error("SP_032", "ReadBytes 不能为负数"));

        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
