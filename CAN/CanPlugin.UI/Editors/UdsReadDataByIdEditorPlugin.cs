using System.Windows;
using CAN.UI.Validation;
using CAN.UI.Views;
using CAN.UDS;
using CAN.UDS.Models;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
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

	public Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		StepEditorValidationContext context, CancellationToken cancellationToken = default)
	{
		var errors = new List<StepSettingError>();
		var s = (UdsReadDataByIdSetting)new UdsReadDataByIdPlugin().CreateSerializer().Deserialize(context.Setting, 1);

		if (string.IsNullOrWhiteSpace(s.ConnectionName))
			errors.Add(StepSettingError.Error("UDS_001", "ConnectionName 不能为空"));
		else if (!context.Evaluator.ValidateExpression(s.ConnectionName, context.ExecutionContext, out var connErr))
			errors.Add(StepSettingError.Error("UDS_001E", $"ConnectionName 表达式无效: {connErr}"));
		if (string.IsNullOrWhiteSpace(s.TxId))
			errors.Add(StepSettingError.Error("UDS_002", "TX ID 不能为空"));
		else if (!context.Evaluator.ValidateExpression(s.TxId, context.ExecutionContext, out var txErr))
			errors.Add(StepSettingError.Error("UDS_002E", $"TxId 表达式无效: {txErr}"));
		if (string.IsNullOrWhiteSpace(s.RxId))
			errors.Add(StepSettingError.Error("UDS_003", "RX ID 不能为空"));
		else if (!context.Evaluator.ValidateExpression(s.RxId, context.ExecutionContext, out var rxErr))
			errors.Add(StepSettingError.Error("UDS_003E", $"RxId 表达式无效: {rxErr}"));
		if (s.ResponseTimeoutMs == 0 || s.ResponseTimeoutMs < -1)
			errors.Add(StepSettingError.Error("UDS_005", "响应超时必须大于 0，或为 -1 表示永不超时"));
		if (string.IsNullOrWhiteSpace(s.Did))
			errors.Add(StepSettingError.Error("UDS_D001", "DID 不能为空"));
		else if (!context.Evaluator.ValidateExpression(s.Did, context.ExecutionContext, out var didErr))
			errors.Add(StepSettingError.Error("UDS_D001E", $"Did 表达式无效: {didErr}"));

		if (!string.IsNullOrWhiteSpace(s.ResultVariable))
		{
			if (!context.ExecutionContext.HasVariable(s.ResultVariable))
				errors.Add(StepSettingError.Error("UDS_D002", $"变量 {s.ResultVariable} 不存在，请先创建该变量"));
			else
			{
				var val = context.ExecutionContext.GetVariable(s.ResultVariable);
				if (val is not null && val is not string)
					errors.Add(StepSettingError.Warning("UDS_D003", $"变量 {s.ResultVariable} 类型不匹配，期望 string，实际类型 {val.GetType().Name}"));
			}
		}

		CanLifecycleValidator.CheckPrecedingOpen(context.SequenceFile, context.Block, context.CurrentStep, s.ConnectionName, errors);

		return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
	}
}