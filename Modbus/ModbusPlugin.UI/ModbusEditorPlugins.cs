using System.Windows;
using Modbus.Models;
using Modbus.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace Modbus.UI;

/// <summary>
/// Modbus 连接步骤的编辑器插件，提供 UI 编辑器创建和设置校验
/// </summary>
public sealed class ModbusConnectEditorPlugin : IStepEditorPlugin
{
	public string StepTypeId => "IO.ModbusConnect";
	public string IconPath => "pack://application:,,,/Modbus.StepPlugin.UI;component/Resources/Icons/modbus.png";

	public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
	{
		var view = new ModbusConnectEditorView();
		view.ViewModel.AttachSerializer(new ModbusConnectPlugin().CreateSerializer());
		view.ViewModel.AttachStep(step);
		return view;
	}

	public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct = default)
	{
		var errors = new List<StepSettingError>();
		var s = (ModbusConnectSetting)new ModbusConnectPlugin().CreateSerializer().Deserialize(setting, 1);
		if (string.IsNullOrWhiteSpace(s.ConnectionName))
			errors.Add(StepSettingError.Error("MB_001", "连接标识名不能为空"));
		if (s.TransportType == ModbusTransportType.TCP && string.IsNullOrWhiteSpace(s.IpAddress))
			errors.Add(StepSettingError.Error("MB_002", "TCP 模式下 IP 地址不能为空"));
		if (s.TransportType == ModbusTransportType.RTU && string.IsNullOrWhiteSpace(s.PortName))
			errors.Add(StepSettingError.Error("MB_003", "RTU 模式下串口名称不能为空"));
		return errors;
	}
}

public sealed class ModbusDisconnectEditorPlugin : IStepEditorPlugin
{
	public string StepTypeId => "IO.ModbusDisconnect";
	public string IconPath => "pack://application:,,,/Modbus.StepPlugin.UI;component/Resources/Icons/modbus.png";

	public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
	{
		var view = new ModbusDisconnectEditorView();
		view.ViewModel.AttachSerializer(new ModbusDisconnectPlugin().CreateSerializer());
		view.ViewModel.AttachStep(step);
		return view;
	}

	public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct = default)
	{
		var errors = new List<StepSettingError>();
		var s = (ModbusDisconnectSetting)new ModbusDisconnectPlugin().CreateSerializer().Deserialize(setting, 1);
		if (string.IsNullOrWhiteSpace(s.ConnectionName))
			errors.Add(StepSettingError.Error("MB_010", "连接标识名不能为空"));
		return errors;
	}
}

public sealed class ModbusReadEditorPlugin : IStepEditorPlugin
{
	public string StepTypeId => "IO.ModbusRead";
	public string IconPath => "pack://application:,,,/Modbus.StepPlugin.UI;component/Resources/Icons/modbus.png";

	public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
	{
		var view = new ModbusReadEditorView();
		view.ViewModel.AttachSerializer(new ModbusReadPlugin().CreateSerializer());
		view.ViewModel.AttachStep(step);
		return view;
	}

	public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct = default)
	{
		var errors = new List<StepSettingError>();
		var s = (ModbusReadSetting)new ModbusReadPlugin().CreateSerializer().Deserialize(setting, 1);
		if (string.IsNullOrWhiteSpace(s.ConnectionName))
			errors.Add(StepSettingError.Error("MB_020", "连接标识名不能为空"));
		if (string.IsNullOrWhiteSpace(s.ResultVariable))
			errors.Add(StepSettingError.Error("MB_021", "结果变量名不能为空"));
		return errors;
	}
}

public sealed class ModbusWriteEditorPlugin : IStepEditorPlugin
{
	public string StepTypeId => "IO.ModbusWrite";
	public string IconPath => "pack://application:,,,/Modbus.StepPlugin.UI;component/Resources/Icons/modbus.png";

	public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
	{
		var view = new ModbusWriteEditorView();
		view.ViewModel.AttachSerializer(new ModbusWritePlugin().CreateSerializer());
		view.ViewModel.AttachStep(step);
		return view;
	}

	public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct = default)
	{
		var errors = new List<StepSettingError>();
		var s = (ModbusWriteSetting)new ModbusWritePlugin().CreateSerializer().Deserialize(setting, 1);
		if (string.IsNullOrWhiteSpace(s.ConnectionName))
			errors.Add(StepSettingError.Error("MB_030", "连接标识名不能为空"));
		if (string.IsNullOrWhiteSpace(s.Values))
			errors.Add(StepSettingError.Error("MB_031", "写入值不能为空"));
		return errors;
	}
}

public sealed class ModbusBatchReadEditorPlugin : IStepEditorPlugin
{
	public string StepTypeId => "IO.ModbusBatchRead";
	public string IconPath => "pack://application:,,,/Modbus.StepPlugin.UI;component/Resources/Icons/modbus.png";

	public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
	{
		var view = new ModbusBatchReadEditorView();
		view.ViewModel.AttachSerializer(new ModbusBatchReadPlugin().CreateSerializer());
		view.ViewModel.AttachStep(step);
		return view;
	}

	public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct = default)
	{
		var errors = new List<StepSettingError>();
		var s = (ModbusBatchReadSetting)new ModbusBatchReadPlugin().CreateSerializer().Deserialize(setting, 1);
		if (string.IsNullOrWhiteSpace(s.ConnectionName))
			errors.Add(StepSettingError.Error("MB_040", "连接标识名不能为空"));
		if (s.Items.Count == 0)
			errors.Add(StepSettingError.Warning("MB_041", "批量读取列表为空"));
		return errors;
	}
}

public sealed class ModbusBatchWriteEditorPlugin : IStepEditorPlugin
{
	public string StepTypeId => "IO.ModbusBatchWrite";
	public string IconPath => "pack://application:,,,/Modbus.StepPlugin.UI;component/Resources/Icons/modbus.png";

	public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
	{
		var view = new ModbusBatchWriteEditorView();
		view.ViewModel.AttachSerializer(new ModbusBatchWritePlugin().CreateSerializer());
		view.ViewModel.AttachStep(step);
		return view;
	}

	public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		byte[] setting, IExpressionEvaluator evaluator, IExecutionContext context, CancellationToken ct = default)
	{
		var errors = new List<StepSettingError>();
		var s = (ModbusBatchWriteSetting)new ModbusBatchWritePlugin().CreateSerializer().Deserialize(setting, 1);
		if (string.IsNullOrWhiteSpace(s.ConnectionName))
			errors.Add(StepSettingError.Error("MB_050", "连接标识名不能为空"));
		if (s.Items.Count == 0)
			errors.Add(StepSettingError.Warning("MB_051", "批量写入列表为空"));
		return errors;
	}
}