using System.Windows;
using CiA402.Core;
using CiA402.Models;
using CiA402.UI.Validation;
using CiA402.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace CiA402.UI.Editors;

public sealed class HomeEditorPlugin : IStepEditorPlugin
{
	public string StepTypeId => "Motion.CiA402Home";
	public string IconPath => "pack://application:,,,/CiA402.StepPlugin.UI;component/Resources/Icons/cia402_home.png";

	public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
	{
		var view = new HomeEditorView { SequenceFile = sequenceFile };
		view.ViewModel.AttachSerializer(new HomePlugin().CreateSerializer());
		view.ViewModel.AttachStep(step);
		return view;
	}

	public Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(StepEditorValidationContext context, CancellationToken ct = default)
	{
		var errors = new List<StepSettingError>();
		try
		{
			ct.ThrowIfCancellationRequested();
			var serializer = new HomePlugin().CreateSerializer();
			var s = context.Setting is { Length: > 0 } data
				? (HomeSetting)serializer.Deserialize(data, context.CurrentStep.StepSetting.SettingVersion)
				: (HomeSetting)serializer.CreateDefault();
			errors.AddRange(SettingValidation.Validate(s).Select(message => StepSettingError.Error("C402_001", message)));
            EditorValidation.Expression(context, s.Method, "回零方式", errors, -128, 127);
            EditorValidation.Expression(context, s.SearchVelocity, "搜索速度", errors, 1, 4294967295);
            EditorValidation.Expression(context, s.ZeroVelocity, "零点速度", errors, 1, 4294967295);
            EditorValidation.Expression(context, s.Acceleration, "回零加速度", errors, 1, 4294967295);
            EditorValidation.Expression(context, s.AxisName, "轴会话名", errors);
			AxisLifecycleValidator.Validate(context, s, errors);

		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) { errors.Add(StepSettingError.Error("C402_007", $"设置校验失败：{ex.Message}")); }
		return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
	}
}
