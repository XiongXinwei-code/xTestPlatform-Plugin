using System.Windows;
using CAN.UI.Views;
using CAN.UDS;
using CAN.UDS.Models;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI;

public sealed class UdsSecurityAccessEditorPlugin : IStepEditorPlugin
{
	public string StepTypeId => "UDS.SecurityAccess";
	public string IconPath => string.Empty;

	public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
	{
		var view = new UdsSecurityAccessEditorView();
		view.SequenceFile = sequenceFile;
		view.RefreshFromStep(step);
		return view;
	}
}