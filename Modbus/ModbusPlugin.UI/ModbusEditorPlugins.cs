using System.Windows;
using Modbus.Models;
using Modbus.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace Modbus.UI;

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
			errors.Add(StepSettingError.Error("MB_001", "杩炴帴鏍囪瘑鍚嶄笉鑳戒负绌?));
		if (s.TransportType == ModbusTransportType.TCP && string.IsNullOrWhiteSpace(s.IpAddress))
			errors.Add(StepSettingError.Error("MB_002", "IP 鍦板潃涓嶈兘涓虹┖"));
		if (s.TransportType == ModbusTransportType.RTU && string.IsNullOrWhiteSpace(s.PortName))
			errors.Add(StepSettingError.Error("MB_003", "涓插彛鍚嶇О涓嶈兘涓虹┖"));
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
			errors.Add(StepSettingError.Error("MB_010", "杩炴帴鏍囪瘑鍚嶄笉鑳戒负绌?));
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
			errors.Add(StepSettingError.Error("MB_020", "杩炴帴鏍囪瘑鍚嶄笉鑳戒负绌?));
		if (string.IsNullOrWhiteSpace(s.ResultVariable))
			errors.Add(StepSettingError.Error("MB_021", "缁撴灉鍙橀噺鍚嶄笉鑳戒负绌?));
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
			errors.Add(StepSettingError.Error("MB_030", "杩炴帴鏍囪瘑鍚嶄笉鑳戒负绌?));
		if (string.IsNullOrWhiteSpace(s.Values))
			errors.Add(StepSettingError.Error("MB_031", "鍐欏叆鍊间笉鑳戒负绌?));
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
			errors.Add(StepSettingError.Error("MB_040", "杩炴帴鏍囪瘑鍚嶄笉鑳戒负绌?));
		if (s.Items.Count == 0)
			errors.Add(StepSettingError.Warning("MB_041", "鎵归噺璇诲彇鍒楄〃涓虹┖"));
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
			errors.Add(StepSettingError.Error("MB_050", "杩炴帴鏍囪瘑鍚嶄笉鑳戒负绌?));
		if (s.Items.Count == 0)
			errors.Add(StepSettingError.Warning("MB_051", "鎵归噺鍐欏叆鍒楄〃涓虹┖"));
		return errors;
	}
}
