using System.Windows;
using CiA402.Core;
using CiA402.Models;
using CiA402.UI.Validation;
using CiA402.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace CiA402.UI.Editors;

public sealed class ReadDiagnosticsEditorPlugin : IStepEditorPlugin
{
	public string StepTypeId => "Motion.CiA402ReadDiagnostics";
	public string IconPath => "pack://application:,,,/CiA402.StepPlugin.UI;component/Resources/Icons/cia402_readdiagnostics.png";

	public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
	{
		var view = new ReadDiagnosticsEditorView { SequenceFile = sequenceFile };
		view.ViewModel.AttachSerializer(new ReadDiagnosticsPlugin().CreateSerializer());
		view.ViewModel.AttachStep(step);
		return view;
	}

	public Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(StepEditorValidationContext context, CancellationToken ct = default)
	{
		var errors = new List<StepSettingError>();
		try
		{
			ct.ThrowIfCancellationRequested();
			var serializer = new ReadDiagnosticsPlugin().CreateSerializer();
			var s = context.Setting is { Length: > 0 } data
				? (ReadDiagnosticsSetting)serializer.Deserialize(data, context.CurrentStep.StepSetting.SettingVersion)
				: (ReadDiagnosticsSetting)serializer.CreateDefault();
			errors.AddRange(SettingValidation.Validate(s).Select(message => StepSettingError.Error("C402_001", message)));
            EditorValidation.Output(context, s.ErrorCodeVariable, typeof(double), errors);
            EditorValidation.Output(context, s.HardwareValueVariable, typeof(double), errors);
            EditorValidation.Output(context, s.HardwareActiveVariable, typeof(bool), errors);
            EditorValidation.Expression(context, s.AxisName, "轴会话名", errors);
			AxisLifecycleValidator.Validate(context, s, errors);
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) { errors.Add(StepSettingError.Error("C402_007", $"设置校验失败：{ex.Message}")); }
		return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
	}
}
