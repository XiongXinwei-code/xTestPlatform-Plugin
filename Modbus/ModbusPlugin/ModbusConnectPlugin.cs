using Modbus.Executors;
using Modbus.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Modbus;

/// <summary>
/// Modbus 连接插件，支持 TCP 和 RTU(串口) 两种传输方式
/// </summary>
public sealed class ModbusConnectPlugin : StepPluginBase<ModbusConnectSetting>, IStepPlugin
{
	public override string StepTypeId => "IO.ModbusConnect";
	public override string DisplayName => "Modbus_Connect";
	public override string Category => "Communication";
	public override string IconPath => "pack://application:,,,/Modbus.StepPlugin.UI;component/Resources/Icons/modbus.png";

	public override string Description => """
		## 功能

		建立 Modbus 连接，支持 TCP 和 RTU（串口）两种传输方式，连接成功后通过 ConnectionName 标识连接。

		## 参数

		| 参数 | 类型 | 必填 | 默认值 | 说明 |
		|------|------|------|--------|------|
		| ConnectionName | string([ExpressionField]) | 是 | Modbus1 | 连接标识名，序列内唯一 |
		| TransportType | 枚举 | 是 | TCP | 可选值：TCP, RTU |
		| IpAddress | string([ExpressionField]) | TCP 时 | 127.0.0.1 | TCP 服务器地址 |
		| TcpPort | int | 否 | 502 | TCP 端口 |
		| PortName | string([ExpressionField]) | RTU 时 | — | 串口名，如 COM1 |
		| BaudRate | int | 否 | 9600 | 波特率（RTU） |
		| DataBits | int | 否 | 8 | 数据位（RTU） |
		| StopBits | int | 否 | 1 | 停止位（RTU） |
		| Parity | int | 否 | 0 | 校验位：0=None, 1=Odd, 2=Even（RTU） |
		| TimeoutMs | int | 否 | 3000 | 通信超时毫秒数 |

		## 行为

		- TransportType=TCP 时使用 IpAddress/TcpPort，RTU 时使用串口参数
		- 连接失败或同名连接已存在时步骤报错
		- 仅支持 TCP 与 RTU 两种传输方式，不支持 Modbus ASCII

		## 检索关键词

		Modbus、Modbus TCP、Modbus RTU、模总线、
		从站地址、从机地址、Slave ID、Unit ID、功能码、
		RS-485、RS485、串口从站、PLC 通信、仪表采集

		寄存器与线圈（保持寄存器、输入寄存器、线圈、离散输入）的选择在 `Modbus_Read` / `Modbus_Write` 步骤中配置，不在本步骤。

		## 相关插件

		- `Modbus_Read` / `Modbus_Write` / `Modbus_BatchRead` / `Modbus_BatchWrite`：在此连接上读写数据
		- `Modbus_Disconnect`：关闭本插件建立的连接
		""";

	public override IStepExecutor CreateExecutor() => new ModbusConnectExecutor();

	public override string GenerateDescription(byte[] setting)
	{
		var s = DeserializeSetting(setting);
		return s.TransportType == ModbusTransportType.TCP
			? $"Connect {s.ConnectionName} (TCP: {s.IpAddress}:{s.TcpPort})"
			: $"Connect {s.ConnectionName} (RTU: {s.PortName} @ {s.BaudRate})";
	}

	public Task<IReadOnlyList<StepSettingError>> ValidateSettingAsync(
		StepSettingValidationContext context,
		CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		var errors = new List<StepSettingError>();

		ModbusConnectSetting s;
		try
		{
			s = DeserializeSetting(context.Setting, context.CurrentStep.StepSetting.SettingVersion);
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex)
		{
			errors.Add(StepSettingError.Error("MB_00X", $"设置无法读取：{ex.Message}"));
			return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
		}

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

		return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
	}
}