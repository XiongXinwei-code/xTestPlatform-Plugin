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

public sealed class SerialPortWriteEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "SerialPort.Write";
    public string IconPath => string.Empty;

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new SerialPortWriteEditorView();
        view.ViewModel.AttachSerializer(new SerialPortWritePlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (SerialPortWriteSetting)new SerialPortWritePlugin().CreateSerializer().Deserialize(setting, 1);
        if (string.IsNullOrWhiteSpace(s.Data))
            errors.Add(StepSettingError.Warning("SP_W01", "发送数据为空"));
        return errors;
    }
}
