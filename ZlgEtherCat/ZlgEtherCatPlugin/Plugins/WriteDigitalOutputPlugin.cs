using ZlgEtherCat.Executors;
using ZlgEtherCat.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace ZlgEtherCat.Plugins;

public sealed class WriteDigitalOutputPlugin : ZlgEtherCat.PluginInfrastructure.StrictStepPluginBase<WriteDigitalOutputSetting>
{
    public override string StepTypeId => "ZlgEtherCat.WriteDigitalOutput";
    public override string DisplayName => "ZlgEtherCat_IO_WriteDigitalOutput";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/ZlgEtherCat.StepPlugin.UI;component/Resources/Icons/zlg_writedigitaloutput.png";
    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("ZlgEtherCat", () => new WriteDigitalOutputExecutor());
    public override string GenerateDescription(byte[] setting)
    {
        try
        {
            var s = (WriteDigitalOutputSetting)CreateSerializer().Deserialize(setting, 1);
            return s.Operation switch
            {
                WriteDigitalOutputOperation.WriteDigitalOutput => $"写入数字量输出：连接名 {s.ConnectionName}；从站 ENI 索引（常量） {(string.IsNullOrWhiteSpace(s.SlaveIndexExpression) ? s.SlaveIndex.ToString() : s.SlaveIndexExpression)}；从站输出区相对字节偏移 {s.Offset}；输出值 {(string.IsNullOrWhiteSpace(s.ValueExpression) ? s.Value.ToString() : s.ValueExpression)}；结果变量 {s.ResultVariable}",
                _ => "写入数字量输出（所选功能未定义）"
            };
        }
        catch (Exception ex) { return "写入数字量输出（设置无法解析：" + ex.Message + "）"; }
    }
    public override string Description => """
        ## 功能
        写入数字量输出。本步骤为固定功能，只执行 WriteDigitalOutput，无需选择操作；编辑参数不会执行设备动作。

        ## 参数
        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        | --- | --- | --- | --- | --- |
        | Operation | WriteDigitalOutputOperation | 固定值，无需选择 | WriteDigitalOutput | WriteDigitalOutput=0（写入数字量输出）；固定功能，不接受其他操作 |
        | ConnectionName | string([ExpressionField]) | 是 | "ECAT1" | WriteDigitalOutput；连接名；求值结果为 string；选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名 |
        | SlaveIndex | int | 是 | 0 | WriteDigitalOutput；从站 ENI 索引（常量）；本连接加载 ENI 中从 0 开始的 SlaveIdx；不是接线 PhysicalPosition、站地址、别名或同型号序号。先用 EnumerateSlaves 核对；SlaveIndexExpression 非空时此常量不生效。；范围 0～65534 |
        | SlaveIndexExpression | string([ExpressionField]) | 否 |  | WriteDigitalOutput；从站 ENI 索引表达式（可选）；求值结果为 int；留空使用上方从站常量；非空时优先求值。结果为本连接 ENI 中从 0 开始的 SlaveIdx，不是物理站位、站地址或别名。范围 0～65534。；范围 0～65534 |
        | Offset | int | 是 | 0 | WriteDigitalOutput；从站输出区相对字节偏移；相对指定从站输出映像起点，从 0 开始。不是全局 Offset，也不是对象索引；实际绝对位置=ReadPiRange 中输出 Offset+此值。；范围 0～16777215 |
        | BitOffset | int | 是 | 0 | WriteDigitalOutput；输出位；LSB0 位编号：0 为该字节最低位，7 为最高位；不等同于模块端子号，端子到位的对应关系由设备 PDO 映射确定；范围 0～7 |
        | Value | bool | 是 | false | WriteDigitalOutput；输出值；兼容常量；ValueExpression 非空时使用表达式结果。false 清除选定位，true 置位；不覆盖同字节其他位。 |
        | ValueExpression | string([ExpressionField]) | 否 |  | WriteDigitalOutput；输出值表达式（可选）；求值结果为 bool；留空使用上方常量；非空时用此表达式的结果作为本次输出，避免改写步骤配置。 |
        | TimeoutMs | int | 是 | 5000 | WriteDigitalOutput；步骤总超时（ms）；包括排队、表达式和执行等待；超时不证明原生调用已退出；范围 1～3600000 |
        | ResultVariable | string(VariablePathField) | 否 |  | WriteDigitalOutput；结果变量；可选已声明变量完整路径；bool：true 表示本次配置或软件操作完成，不证明物理动作完成。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。 |

        ## 行为
        - 平台助手直接读写本步骤参数；Operation 为唯一固定值，无需选择。全部参数公开可读写、保存，不需要 UI 私有状态或 JSON 脚本。
        - 本步骤只执行其固定功能的参数校验、表达式求值及设备调用；打开、关闭、启动、停止分别由各自独立步骤执行。
        - 当前平台宿主可能预编译表达式字段；预编译不求值、不触发设备动作。此步骤只包含其固定操作的参数。
        - ResultVariable 是已声明变量路径，保存原始返回值；步骤结果显示 JSON。TimeoutMs 为本步骤配置总期限。
        - 超时和失败返回带中文 ErrorInfo 的 Error；仅原调用方取消返回 Aborted。SDK 提交完成不证明设备已停止。
        ### WriteDigitalOutput — 写入数字量输出
        - 适用参数：ConnectionName、TimeoutMs、SlaveIndex、SlaveIndexExpression、Offset、BitOffset、Value、ValueExpression、ResultVariable。

        - 目标与范围：目标为 ConnectionName + ENI 零基 SlaveIndex 选择的单个从站；同型号模块按不同 ENI 索引分别配置。
        - 前置操作：ZlgEtherCat.EnablePi；编辑器只提示静态顺序，执行时仍检查真实状态。
        - 返回值：bool：true 表示本次配置或软件操作完成，不证明物理动作完成。
        -  执行时必须已确认 OP；StartBus 的 RequireDc=true 时还须确认 DC 内部同步与传播延迟初始化。 任一轴绑定了目标从站时，拒绝该从站的全部原始输出写入；其他从站的输入读取不受此门禁限制。 BeginExit 后拒绝本操作；完成关闭并重新打开连接后才可开展新的业务。

        ## 相关插件
        ZlgEtherCat.EnablePi、ZlgEtherCat.Open。
        """;
}
