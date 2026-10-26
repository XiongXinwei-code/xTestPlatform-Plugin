using Modbus.Executors;
using Modbus.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace Modbus;

/// <summary>
/// Modbus 断开连接插件，关闭并释放指定的 Modbus 连接资源
/// </summary>
public sealed class ModbusDisconnectPlugin : StepPluginBase<ModbusDisconnectSetting>
{
	public override string StepTypeId => "IO.ModbusDisconnect";
	public override string DisplayName => "Modbus_Disconnect";
	public override string Category => "Communication";
	public override string IconPath => "pack://application:,,,/Modbus.StepPlugin.UI;component/Resources/Icons/modbus_disconnect.png";

	public override string Description => """
		## 功能

		关闭指定的 Modbus 连接并释放资源。

		## 参数

		| 参数 | 类型 | 必填 | 默认值 | 说明 |
		|------|------|------|--------|------|
		| ConnectionName | string([ExpressionField]) | 是 | — | 要关闭的 Modbus 连接标识名，求值结果为 string |
		| TimeoutMs | int | 否 | 0 | 0 继承连接通信期限，正数覆盖本次关闭 |

		## 行为


		- 先冻结当前代次的新操作，再等待真实任务和 SDK 释放；只在确认释放后移除自己的资源。超时/取消不代表底层已退出，未完成或关闭失败的 owner 保留诊断与重试入口；立即重跑将按打开/配置步骤期限等待旧收尾，不强制关闭其他活动连接。

		- 从运行期资源表中移除 `ConnectionName` 对应的主站对象及其底层传输对象，移除时会自动释放
		- 连接名不存在时**不报错**，按 Completed 返回

		## 相关插件

		- `Modbus_Connect`：建立连接
		""";

	public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("Modbus", () => new ModbusDisconnectExecutor());

	public override string GenerateDescription(byte[] setting)
	{
		var s = DeserializeSetting(setting);
		return $"Disconnect {s.ConnectionName}";
	}
}
