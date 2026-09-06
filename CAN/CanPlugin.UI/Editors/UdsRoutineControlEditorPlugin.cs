using System.Windows;
using CAN.UI.Views;
using CAN.UDS;
using CAN.UDS.Models;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI;

public sealed class UdsRoutineControlEditorPlugin : IStepEditorPlugin
{
	public string StepTypeId => "UDS.RoutineControl";
	public string IconPath => string.Empty;

	public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
	{
		var view = new UdsRoutineControlEditorView();
		view.SequenceFile = sequenceFile;
		view.RefreshFromStep(step);
		return view;
	}
}