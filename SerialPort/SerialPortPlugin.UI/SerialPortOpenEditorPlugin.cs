using System.Windows;
using SerialPortPlugin.Models;
using SerialPortPlugin.Plugins;
using SerialPortPlugin.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace SerialPortPlugin.UI;

public sealed class SerialPortOpenEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "SerialPort.Open";
    public string IconPath => string.Empty;

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new SerialPortOpenEditorView();
        view.ViewModel.AttachSerializer(new SerialPortOpenPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (SerialPortOpenSetting)new SerialPortOpenPlugin().CreateSerializer().Deserialize(setting, 1);
        if (string.IsNullOrWhiteSpace(s.PortName))
            errors.Add(StepSettingError.Error("SP_001", "端口名不能为空"));
        if (s.BaudRate <= 0)
            errors.Add(StepSettingError.Error("SP_002", "波特率必须大于0"));
        return errors;
    }
}
