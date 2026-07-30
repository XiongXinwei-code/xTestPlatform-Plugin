using System.Windows;
using LXI.Models;
using LXI.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace LXI.UI;

public sealed class LxiQueryEditorPlugin : IStepEditorPlugin
{
	public string StepTypeId => "IO.LxiQuery";
	public string IconPath => "pack://application:,,,/LXI.StepPlugin.UI;component/Resources/Icons/lxi.png";

	public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
	{
		var view = new LxiQueryEditorView();
		view.ViewModel.AttachSerializer(new LxiQueryPlugin().CreateSerializer());
		view.ViewModel.AttachStep(step);
		return view;
	}

	public Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct = default)
	{
		var errors = new List<StepSettingError>();
		var s = setting is { Length: > 0 }
			? (LxiQuerySetting)new LxiQueryPlugin().CreateSerializer().Deserialize(setting, 1)
			: new LxiQuerySetting();

		if (string.IsNullOrWhiteSpace(s.IpAddress))
			errors.Add(StepSettingError.Error("LXI_040", "IP 地址不能为空"));

		if (string.IsNullOrWhiteSpace(s.Command))
			errors.Add(StepSettingError.Error("LXI_041", "Command 不能为空"));

		if (string.IsNullOrWhiteSpace(s.ResultVariable))
			errors.Add(StepSettingError.Error("LXI_042", "结果变量不能为空"));
		else if (!context.HasVariable(s.ResultVariable))
			errors.Add(StepSettingError.Error("LXI_043", $"变量 {s.ResultVariable} 不存在"));

		return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
	}
}