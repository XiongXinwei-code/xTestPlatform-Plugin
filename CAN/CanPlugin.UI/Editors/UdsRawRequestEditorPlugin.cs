using System.Windows;
using CAN.UI.Views;
using CAN.UDS;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI;

public sealed class UdsRawRequestEditorPlugin : IStepEditorPlugin
{
	public string StepTypeId => "UDS.RawRequest";
	public string IconPath => string.Empty;

	public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
	{
		var view = new UdsRawRequestEditorView();
		view.RefreshFromStep(step);
		return view;
	}

	public Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		StepEditorValidationContext context, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<IReadOnlyList<StepSettingError>>(Array.Empty<StepSettingError>());
	}
}