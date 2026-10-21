using System.Windows;
using CiA402.Core;
using CiA402.Models;
using CiA402.UI.Validation;
using CiA402.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace CiA402.UI.Editors;

public sealed class BindAxisEditorPlugin : IStepEditorPlugin
{
	public string StepTypeId => "Motion.CiA402BindAxis";
	public string IconPath => "pack://application:,,,/CiA402.StepPlugin.UI;component/Resources/Icons/cia402_bindaxis.png";

	public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
	{
		var view = new BindAxisEditorView { SequenceFile = sequenceFile };
		view.ViewModel.AttachSerializer(new BindAxisPlugin().CreateSerializer());
		view.ViewModel.AttachStep(step);
		return view;
	}

	public Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(StepEditorValidationContext context, CancellationToken ct = default)
	{
		var errors = new List<StepSettingError>();
		try
		{
			ct.ThrowIfCancellationRequested();
			var serializer = new BindAxisPlugin().CreateSerializer();
			var s = context.Setting is { Length: > 0 } data
				? (BindAxisSetting)serializer.Deserialize(data, context.CurrentStep.StepSetting.SettingVersion)
				: (BindAxisSetting)serializer.CreateDefault();
			errors.AddRange(SettingValidation.Validate(s).Select(message => StepSettingError.Error("C402_001", message)));
            EditorValidation.Expression(context, s.ConnectionName, "连接名", errors);
            if (s.Transport == TransportKind.CANopen) EditorValidation.Expression(context, s.NodeId, "CANopen 节点", errors, 1, 127);
            if (s.Transport == TransportKind.EtherCAT) EditorValidation.Expression(context, s.SlaveAddress, "EtherCAT 从站地址", errors, 0, 65535);
            EditorValidation.Expression(context, s.AxisName, "轴会话名", errors);
			AxisLifecycleValidator.Validate(context, s, errors);
            if (s.Transport == TransportKind.EtherCAT) errors.Add(StepSettingError.Warning("C402_006", "EtherCAT 需要运行环境注册主站适配器，本插件不包含主站 SDK"));
		}
		catch (OperationCanceledException) { throw; }
		catch (Exception ex) { errors.Add(StepSettingError.Error("C402_007", $"设置校验失败：{ex.Message}")); }
		return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
	}
}
