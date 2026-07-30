using System.Windows;
using LXI.Models;
using LXI.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace LXI.UI;

public sealed class LxiReadEditorPlugin : IStepEditorPlugin
{
	public string StepTypeId => "IO.LxiRead";
	public string IconPath => "pack://application:,,,/LXI.StepPlugin.UI;component/Resources/Icons/lxi.png";

	public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
	{
		var view = new LxiReadEditorView();
		view.ViewModel.AttachSerializer(new LxiReadPlugin().CreateSerializer());
		view.ViewModel.AttachStep(step);
		return view;
	}

	public Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct = default)
	{
		var errors = new List<StepSettingError>();
		var s = setting is { Length: > 0 }
			? (LxiReadSetting)new LxiReadPlugin().CreateSerializer().Deserialize(setting, 1)
			: new LxiReadSetting();

		if (string.IsNullOrWhiteSpace(s.IpAddress))
			errors.Add(StepSettingError.Error("LXI_030", "IP 地址不能为空"));

		if (string.IsNullOrWhiteSpace(s.ResultVariable))
			errors.Add(StepSettingError.Error("LXI_031", "结果变量不能为空"));
		else if (!context.HasVariable(s.ResultVariable))
			errors.Add(StepSettingError.Error("LXI_032", $"变量 {s.ResultVariable} 不存在"));

		return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
	}
}