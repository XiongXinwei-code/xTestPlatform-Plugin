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
    public string IconPath => "pack://application:,,,/SerialPort.StepPlugin.UI;component/Resources/Icons/serialport.png";

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

        if (s.DataFormat == SerialPortDataFormat.Hex && !string.IsNullOrWhiteSpace(s.WriteData))
        {
            var hex = s.WriteData.Trim().Replace(" ", "");
            if (hex.Length % 2 != 0 || !System.Text.RegularExpressions.Regex.IsMatch(hex, @"^[0-9A-Fa-f]+$"))
                errors.Add(StepSettingError.Warning("SP_022", "HEX 格式数据应为偶数位十六进制字符串（如 48656C6C6F）"));
        }

        SerialPortLifecycleValidator.CheckPrecedingOpen(context.SequenceFile, context.Block, context.CurrentStep, s.PortName, errors);

        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
