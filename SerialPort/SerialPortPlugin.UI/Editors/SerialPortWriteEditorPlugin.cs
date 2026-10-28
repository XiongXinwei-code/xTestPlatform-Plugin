using System.Windows;
using SerialPort.Models;
using SerialPort.UI.Validation;
using SerialPort.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace SerialPort.UI;

public sealed class SerialPortWriteEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.SerialPortWrite";
    public string IconPath => "pack://application:,,,/SerialPort.StepPlugin.UI;component/Resources/Icons/serialport_write.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new SerialPortWriteEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new SerialPortWritePlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		StepEditorValidationContext context, CancellationToken cancellationToken = default)
    {
        var errors = new List<StepSettingError>();
        var serializer = new SerialPortWritePlugin().CreateSerializer();
        var s = context.Setting is { Length: > 0 }
            ? (SerialPortWriteSetting)serializer.Deserialize(context.Setting, 1)
            : new SerialPortWriteSetting();

        if (string.IsNullOrWhiteSpace(s.PortName))
            errors.Add(StepSettingError.Error("SP_020", "PortName 不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.PortName, context.ExecutionContext, out var portErr))
            errors.Add(StepSettingError.Error("SP_020E", $"PortName 表达式无效: {portErr}"));

        if (string.IsNullOrWhiteSpace(s.WriteData))
            errors.Add(StepSettingError.Error("SP_021", "WriteData 不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.WriteData, context.ExecutionContext, out var dataErr))
            errors.Add(StepSettingError.Error("SP_021E", $"WriteData 表达式无效: {dataErr}"));

        // WriteData 是表达式，不能把含引号的脚本或变量名当 HEX 数据验证；运行时解析求值结果。
        if (!Enum.IsDefined(s.DataFormat)) errors.Add(StepSettingError.Error("SP_FORMAT", "不支持的串口数据格式"));

        SerialPortLifecycleValidator.CheckPrecedingOpen(context.SequenceFile, context.Block, context.CurrentStep, s.PortName, errors);

        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
