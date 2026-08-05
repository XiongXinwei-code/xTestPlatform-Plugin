using System.Windows;
using Modbus.Models;
using Modbus.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace Modbus.UI;

public sealed class ModbusConnectEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.ModbusConnect";
    public string IconPath => "pack://application:,,,/Modbus.StepPlugin.UI;component/Resources/Icons/modbus.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new ModbusConnectEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new ModbusConnectPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (ModbusConnectSetting)new ModbusConnectPlugin().CreateSerializer().Deserialize(context.Setting, 1);
        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("MB_001", "连接标识名不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.ConnectionName, context.ExecutionContext, out var connErr))
            errors.Add(StepSettingError.Error("MB_001E", $"ConnectionName 表达式无效: {connErr}"));
        if (s.TransportType == ModbusTransportType.TCP && string.IsNullOrWhiteSpace(s.IpAddress))
            errors.Add(StepSettingError.Error("MB_002", "TCP 模式下 IP 地址不能为空"));
        else if (s.TransportType == ModbusTransportType.TCP && !string.IsNullOrWhiteSpace(s.IpAddress)
            && !context.Evaluator.ValidateExpression(s.IpAddress, context.ExecutionContext, out var ipErr))
            errors.Add(StepSettingError.Error("MB_002E", $"IpAddress 表达式无效: {ipErr}"));
        if (s.TransportType == ModbusTransportType.TCP && (s.TcpPort < 1 || s.TcpPort > 65535))
            errors.Add(StepSettingError.Error("MB_004", "TCP 端口号必须在 1~65535 之间"));
        if (s.TransportType == ModbusTransportType.RTU && string.IsNullOrWhiteSpace(s.PortName))
            errors.Add(StepSettingError.Error("MB_003", "RTU 模式下串口名称不能为空"));
        else if (s.TransportType == ModbusTransportType.RTU && !string.IsNullOrWhiteSpace(s.PortName)
            && !context.Evaluator.ValidateExpression(s.PortName, context.ExecutionContext, out var portErr))
            errors.Add(StepSettingError.Error("MB_003E", $"PortName 表达式无效: {portErr}"));
        if (s.TransportType == ModbusTransportType.RTU && s.BaudRate <= 0)
            errors.Add(StepSettingError.Error("MB_005", "RTU 模式下波特率必须大于 0"));
        if (s.TimeoutMs <= 0)
            errors.Add(StepSettingError.Error("MB_006", "通信超时必须大于 0"));
        return errors;
    }
}
