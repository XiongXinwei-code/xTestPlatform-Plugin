using System.Windows;
using CAN.UI.Views;
using CAN.UDS;
using CAN.UDS.Models;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI;

public sealed class UdsReadDataByIdEditorPlugin : IStepEditorPlugin
{
	public string StepTypeId => "UDS.ReadDataByID";
	public string IconPath => string.Empty;

	public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
	{
		var view = new UdsReadDataByIdEditorView();
		view.SequenceFile = sequenceFile;
		view.RefreshFromStep(step);
		return view;
	}
}