using System.Windows;
using SerialPort.Helpers;
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
	public string IconPath => "pack://application:,,,/SerialPort.StepPlugin.UI;component/Resources/Icons/serialport_query.png";

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
		else if (!context.Evaluator.ValidateExpression(s.PortName, context.ExecutionContext, out var portError))
			errors.Add(StepSettingError.Error("SP_040E", $"PortName 表达式无效: {portError}"));

		if (string.IsNullOrWhiteSpace(s.WriteData))
			errors.Add(StepSettingError.Error("SP_041", "WriteData 不能为空"));
		else if (!context.Evaluator.ValidateExpression(s.WriteData, context.ExecutionContext, out var dataError))
			errors.Add(StepSettingError.Error("SP_041E", $"WriteData 表达式无效: {dataError}"));

		if (s.ReadTimeoutMs == 0 || s.ReadTimeoutMs < -1)
			errors.Add(StepSettingError.Error("SP_044", "ReadTimeout 必须大于 0，或为 -1 表示永不超时"));

		if (s.ReadBytes < 0 || s.ReadBytes > 16 * 1024 * 1024)
			errors.Add(StepSettingError.Error("SP_045", "ReadBytes 必须在 0~16 MiB 之间"));
		if (s.ReadTimeoutMs == -1 && s.ReadBytes == 0 && string.IsNullOrEmpty(s.Terminator))
			errors.Add(StepSettingError.Error("SP_FRAME", "无限读取必须配置字节数或终止符"));
		if (!Enum.IsDefined(s.DataFormat)) errors.Add(StepSettingError.Error("SP_FORMAT", "不支持的串口数据格式"));

		if (!string.IsNullOrWhiteSpace(s.ResultVariable))
		{
			if (!SerialPortHelper.IsScopedOutputPath(s.ResultVariable))
				errors.Add(StepSettingError.Error("SP_042", "结果变量必须为带作用域的完整变量路径，如 Locals.rx；留空不写入"));
			else if (!context.ExecutionContext.HasVariable(s.ResultVariable))
				errors.Add(StepSettingError.Error("SP_043", $"变量 {s.ResultVariable} 不存在"));
			else
			{
				var val = context.ExecutionContext.GetVariable(s.ResultVariable);
				if (val is not null && val is not string)
					errors.Add(StepSettingError.Error("SP_047", $"变量 {s.ResultVariable} 类型不匹配，期望 string，实际类型 {val.GetType().Name}"));
			}
		}

		SerialPortLifecycleValidator.CheckPrecedingOpen(context.SequenceFile, context.Block, context.CurrentStep, s.PortName, errors);

		return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
	}
}
