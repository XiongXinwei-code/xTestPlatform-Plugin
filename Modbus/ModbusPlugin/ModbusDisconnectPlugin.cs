using Modbus.Executors;
using Modbus.Models;
using Modbus.Validation;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Modbus;

/// <summary>
/// Modbus 断开连接插件，关闭并释放指定的 Modbus 连接资源
/// </summary>
public sealed class ModbusDisconnectPlugin : StepPluginBase<ModbusDisconnectSetting>, IStepPlugin
{
	public override string StepTypeId => "IO.ModbusDisconnect";
	public override string DisplayName => "Modbus_Disconnect";
	public override string Category => "Communication";
	public override string IconPath => "pack://application:,,,/Modbus.StepPlugin.UI;component/Resources/Icons/modbus.png";

	public override string Description => """
		## 功能

		关闭指定的 Modbus 连接并释放资源。

		## 参数

		| 参数 | 类型 | 必填 | 默认值 | 说明 |
		|------|------|------|--------|------|
		| ConnectionName | string([ExpressionField]) | 是 | — | 要关闭的 Modbus 连接标识名 |

		## 行为

		- 连接不存在时步骤报错

		## 相关插件

		- `Modbus_Connect`：建立连接
		""";

	public override IStepExecutor CreateExecutor() => new ModbusDisconnectExecutor();

	public override string GenerateDescription(byte[] setting)
	{
		var s = DeserializeSetting(setting);
		return $"Disconnect {s.ConnectionName}";
	}

	public Task<IReadOnlyList<StepSettingError>> ValidateSettingAsync(
		StepSettingValidationContext context,
		CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		var errors = new List<StepSettingError>();

		ModbusDisconnectSetting s;
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
			errors.Add(StepSettingError.Error("MB_01X", $"设置无法读取：{ex.Message}"));
			return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
		}

		if (string.IsNullOrWhiteSpace(s.ConnectionName))
			errors.Add(StepSettingError.Error("MB_010", "连接标识名不能为空"));

		ModbusLifecycleValidator.CheckPrecedingConnect(
			context.SequenceFile, context.Block, context.CurrentStep, s.ConnectionName, errors);
		return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
	}
}