using System.Windows;
using CAN.UI.Validation;
using CAN.UI.Views;
using CAN.UDS;
using CAN.UDS.Models;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI;

public sealed class UdsWriteDataByIdEditorPlugin : IStepEditorPlugin
{
	public string StepTypeId => "UDS.WriteDataByID";
	public string IconPath => string.Empty;

	public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
	{
		var view = new UdsWriteDataByIdEditorView();
		view.SequenceFile = sequenceFile;
		view.RefreshFromStep(step);
		return view;
	}

	public Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		StepEditorValidationContext context, CancellationToken cancellationToken = default)
	{
		var errors = new List<StepSettingError>();
		var s = (UdsWriteDataByIdSetting)new UdsWriteDataByIdPlugin().CreateSerializer().Deserialize(context.Setting, 1);

		if (string.IsNullOrWhiteSpace(s.ConnectionName))
			errors.Add(StepSettingError.Error("UDS_001", "ConnectionName 涓嶈兘涓虹┖"));

		CanLifecycleValidator.CheckPrecedingOpen(context.SequenceFile, context.Block, context.CurrentStep, s.ConnectionName, errors);

		return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
	}
}