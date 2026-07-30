using System.Windows;
using LXI.Models;
using LXI.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace LXI.UI;

public sealed class LxiOpenEditorPlugin : IStepEditorPlugin
{
	public string StepTypeId => "IO.LxiOpen";
	public string IconPath => "pack://application:,,,/LXI.StepPlugin.UI;component/Resources/Icons/lxi.png";

	public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
	{
		var view = new LxiOpenEditorView();
		view.ViewModel.AttachSerializer(new LxiOpenPlugin().CreateSerializer());
		view.ViewModel.AttachStep(step);
		return view;
	}

	public Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct = default)
	{
		var errors = new List<StepSettingError>();
		var s = setting is { Length: > 0 }
			? (LxiOpenSetting)new LxiOpenPlugin().CreateSerializer().Deserialize(setting, 1)
			: new LxiOpenSetting();

		if (string.IsNullOrWhiteSpace(s.IpAddress))
			errors.Add(StepSettingError.Error("LXI_001", "IP 地址不能为空"));

		if (s.Port <= 0 || s.Port > 65535)
			errors.Add(StepSettingError.Error("LXI_002", "端口号必须在 1-65535 之间"));

		if (s.ConnectTimeoutMs <= 0)
			errors.Add(StepSettingError.Warning("LXI_003", "连接超时应大于 0"));

		return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
	}
}