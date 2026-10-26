using OpcUa.Executors;
using OpcUa.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace OpcUa;

/// <summary>OPC UA 写入节点插件</summary>
public sealed class OpcUaWritePlugin : StepPluginBase<OpcUaWriteSetting>
{
    public override string StepTypeId => "OpcUa.Write";
    public override string DisplayName => "OpcUa_Write";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/OpcUa.StepPlugin.UI;component/Resources/Icons/opcua_write.png";

    public override string Description => """
        ## 功能

        向 OPC UA 服务器中单个节点写入指定值。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | — | 已建立的 OPC UA 连接名，求值结果为 string |
        | NodeId | string | 是 | — | 节点标识，如 ns=2;s=SetPoint |
        | WriteValue | string([ExpressionField]) | 是 | — | 要写入的值，求值结果为 string |
        | DataType | 枚举 | 否 | Auto | 可选值：Auto, Boolean, Int16, UInt16, Int32, UInt32, Int64, UInt64, Float, Double, String |
        | TimeoutMs | int | 否 | 3000 | Auto 类型读取和写入共用的总期限毫秒数，必须大于 0 |

        ## 行为

        - DataType=Auto 时根据节点实际类型自动转换写入值
        - Auto 与显式数值类型统一使用 InvariantCulture（小数点为 `.`），不接受浮点千位分隔、NaN 或无穷；避免 Windows 区域设置改变实际写值
        - Auto 类型读取晚到时，若本次期限、调用方取消或关闭请求已发生，不再提交后续 Write；已经提交的原始请求及取消回调真实退出前仍由连接持有
        - 连接不存在、节点无效或写入失败时步骤报错

        ## 相关插件

        - `OpcUa_Connect`：建立连接
        - `OpcUa_BatchWrite`：批量写入多个节点
        """;

    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("OpcUa", () => new OpcUaWriteExecutor());

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Write {s.NodeId} = {s.WriteValue}";
    }
}
