using System.Windows;
using SerialPort.Models;
using SerialPort.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace SerialPort.UI;

public sealed class SerialPortOpenEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.SerialPortOpen";
    public string IconPath => "pack://application:,,,/SerialPort.StepPlugin.UI;component/Resources/Icons/serialport.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new SerialPortOpenEditorView();
        view.ViewModel.AttachSerializer(new SerialPortOpenPlugin().CreateSerializer());
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
        var serializer = new SerialPortOpenPlugin().CreateSerializer();
        var s = setting is { Length: > 0 }
            ? (SerialPortOpenSetting)serializer.Deserialize(setting, 1)
            : new SerialPortOpenSetting();

        if (string.IsNullOrWhiteSpace(s.PortName))
            errors.Add(StepSettingError.Error("SP_001", "PortName 不能为空"));

        if (s.BaudRate <= 0)
            errors.Add(StepSettingError.Error("SP_002", "BaudRate 必须大于 0"));

        if (s.DataBits < 5 || s.DataBits > 8)
            errors.Add(StepSettingError.Error("SP_003", "DataBits 必须在 5~8 之间"));

        if (s.StopBits < 0 || s.StopBits > 3)
            errors.Add(StepSettingError.Error("SP_004", "StopBits 值无效"));

        if (s.Parity < 0 || s.Parity > 4)
            errors.Add(StepSettingError.Error("SP_005", "Parity 值无效"));

        if (s.ReadTimeoutMs < 0)
            errors.Add(StepSettingError.Warning("SP_006", "ReadTimeout 不应为负数"));

        if (s.WriteTimeoutMs < 0)
            errors.Add(StepSettingError.Warning("SP_007", "WriteTimeout 不应为负数"));

        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
