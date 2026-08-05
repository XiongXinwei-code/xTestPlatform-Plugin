using System.Windows;
using SerialPort.Models;
using SerialPort.UI.Validation;
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
		view.SequenceFile = sequenceFile;
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

		if (s.ReadTimeoutMs == 0 || s.ReadTimeoutMs < -1)
			errors.Add(StepSettingError.Error("SP_044", "ReadTimeout 必须大于 0，或为 -1 表示永不超时"));

		if (s.ReadBytes < 0)
			errors.Add(StepSettingError.Error("SP_045", "ReadBytes 不能为负数"));

		if (s.DataFormat == SerialPortDataFormat.Hex && !string.IsNullOrWhiteSpace(s.WriteData))
		{
			var hex = s.WriteData.Trim().Replace(" ", "");
			if (hex.Length % 2 != 0 || !System.Text.RegularExpressions.Regex.IsMatch(hex, @"^[0-9A-Fa-f]+$"))
				errors.Add(StepSettingError.Warning("SP_046", "HEX 格式数据应为偶数位十六进制字符串（如 48656C6C6F）"));
		}

		if (string.IsNullOrWhiteSpace(s.ResultVariable))
			errors.Add(StepSettingError.Error("SP_042", "结果变量不能为空"));
		else if (!context.ExecutionContext.HasVariable(s.ResultVariable))
			errors.Add(StepSettingError.Error("SP_043", $"变量 {s.ResultVariable} 不存在"));
		else
		{
			var val = context.ExecutionContext.GetVariable(s.ResultVariable);
			if (val is not null && val is not string)
				errors.Add(StepSettingError.Error("SP_047", $"变量 {s.ResultVariable} 类型不匹配，期望 string，实际类型 {val.GetType().Name}"));
		}

		SerialPortLifecycleValidator.CheckPrecedingOpen(context.SequenceFile, context.Block, context.CurrentStep, s.PortName, errors);

		return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
	}
}