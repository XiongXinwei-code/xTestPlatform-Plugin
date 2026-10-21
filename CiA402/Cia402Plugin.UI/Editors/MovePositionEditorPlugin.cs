using System.Windows;
using CiA402.Core;
using CiA402.Models;
using CiA402.UI.Validation;
using CiA402.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace CiA402.UI.Editors;

public sealed class MovePositionEditorPlugin : IStepEditorPlugin
{
	public string StepTypeId => "Motion.CiA402MovePosition";
	public string IconPath => "pack://application:,,,/CiA402.StepPlugin.UI;component/Resources/Icons/cia402_moveposition.png";

	public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
	{
		var view = new MovePositionEditorView { SequenceFile = sequenceFile };
		view.ViewModel.AttachSerializer(new MovePositionPlugin().CreateSerializer());
		view.ViewModel.AttachStep(step);
		return view;
	}

	public Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(StepEditorValidationContext context, CancellationToken ct = default)
	{
		var errors = new List<StepSettingError>();
		try
		{
			ct.ThrowIfCancellationRequested();
			var serializer = new MovePositionPlugin().CreateSerializer();
			var s = context.Setting is { Length: > 0 } data
				? (MovePositionSetting)serializer.Deserialize(data, context.CurrentStep.StepSetting.SettingVersion)
				: (MovePositionSetting)serializer.CreateDefault();
			errors.AddRange(SettingValidation.Validate(s).Select(message => StepSettingError.Error("C402_001", message)));
            EditorValidation.Expression(context, s.TargetPosition, "目标位置", errors, -2147483648, 2147483647);
            EditorValidation.Expression(context, s.ProfileVelocity, "轮廓速度", errors, 1, 4294967295);
            EditorValidation.Expression(context, s.Acceleration, "加速度", errors, 1, 4294967295);
            EditorValidation.Expression(context, s.Deceleration, "减速度", errors, 1, 4294967295);
            EditorValidation.Expression(context, s.AxisName, "轴会话名", errors);
			AxisLifecycleValidator.Validate(context, s, errors);

		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) { errors.Add(StepSettingError.Error("C402_007", $"设置校验失败：{ex.Message}")); }
		return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
	}
}
