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

public sealed class SerialPortReadEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "SerialPort.Read";
    public string IconPath => string.Empty;

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new SerialPortReadEditorView();
        view.ViewModel.AttachSerializer(new SerialPortReadPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
        byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (SerialPortReadSetting)new SerialPortReadPlugin().CreateSerializer().Deserialize(setting, 1);
        if (string.IsNullOrWhiteSpace(s.TargetVariable))
            errors.Add(StepSettingError.Warning("SP_W02", "目标变量未配置，读取结果将不会保存"));
        return errors;
    }
}
