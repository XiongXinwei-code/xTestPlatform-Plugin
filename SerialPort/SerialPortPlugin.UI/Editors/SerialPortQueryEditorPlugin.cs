using System.Windows;
using SerialPort.Models;
using SerialPort.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace SerialPort.UI;

public sealed class SerialPortQueryEditorPlugin : IStepEditorPlugin
{
	public string StepTypeId => "IO.SerialPortQuery";
	public string IconPath => "pack://application:,,,/SerialPort.StepPlugin.UI;component/Resources/Icons/serialport.png";

	public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
	{
		var view = new SerialPortQueryEditorView();
		view.ViewModel.AttachSerializer(new SerialPortQueryPlugin().CreateSerializer());
		view.ViewModel.AttachStep(step);
		return view;
	}

	public Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		StepEditorValidationContext context, CancellationToken cancellationToken = default)
	{
		var errors = new List<StepSettingError>();
		var serializer = new SerialPortQueryPlugin().CreateSerializer();
		var s = context.Setting is { Length: > 0 }
			? (SerialPortQuerySetting)serializer.Deserialize(context.Setting, 1)
			: new SerialPortQuerySetting();

		if (string.IsNullOrWhiteSpace(s.PortName))
			errors.Add(StepSettingError.Error("SP_040", "PortName 不能为空"));

		if (string.IsNullOrWhiteSpace(s.WriteData))
			errors.Add(StepSettingError.Error("SP_041", "WriteData 不能为空"));

		if (string.IsNullOrWhiteSpace(s.ResultVariable))
			errors.Add(StepSettingError.Error("SP_042", "结果变量不能为空"));
		else if (!context.ExecutionContext.HasVariable(s.ResultVariable))
			errors.Add(StepSettingError.Error("SP_043", $"变量 {s.ResultVariable} 不存在"));

		return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
	}
}