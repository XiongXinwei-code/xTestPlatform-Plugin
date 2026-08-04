using System.Windows;
using SerialPort.Models;
using SerialPort.UI.Validation;
using SerialPort.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

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
		StepEditorValidationContext context, CancellationToken cancellationToken = default)
    {
        var errors = new List<StepSettingError>();
        var serializer = new SerialPortReadPlugin().CreateSerializer();
        var s = context.Setting is { Length: > 0 }
            ? (SerialPortReadSetting)serializer.Deserialize(context.Setting, 1)
            : new SerialPortReadSetting();

        if (string.IsNullOrWhiteSpace(s.PortName))
            errors.Add(StepSettingError.Error("SP_030", "PortName 不能为空"));

        if (s.ReadTimeoutMs <= 0)
            errors.Add(StepSettingError.Error("SP_031", "ReadTimeout 必须大于 0"));

        if (s.ReadBytes < 0)
            errors.Add(StepSettingError.Error("SP_032", "ReadBytes 不能为负数"));

        if (string.IsNullOrWhiteSpace(s.ResultVariable))
            errors.Add(StepSettingError.Error("SP_033", "ResultVariable 未配置，必须指定读取结果存放的变量路径"));
        else if (!context.ExecutionContext.HasVariable(s.ResultVariable))
            errors.Add(StepSettingError.Error("SP_034", $"变量 {s.ResultVariable} 不存在，请先创建该变量"));

        SerialPortLifecycleValidator.CheckPrecedingOpen(context.Block, context.CurrentStep, s.PortName, errors);

        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
