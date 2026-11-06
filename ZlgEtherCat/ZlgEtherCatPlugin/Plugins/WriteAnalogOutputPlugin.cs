using ZlgEtherCat.Executors;
using ZlgEtherCat.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace ZlgEtherCat.Plugins;

public sealed class WriteAnalogOutputPlugin : ZlgEtherCat.PluginInfrastructure.StrictStepPluginBase<WriteAnalogOutputSetting>
{
    public override string StepTypeId => "ZlgEtherCat.WriteAnalogOutput";
    public override string DisplayName => "ZlgEtherCat_IO_WriteAnalogOutput";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/ZlgEtherCat.StepPlugin.UI;component/Resources/Icons/zlg_writeanalogoutput.png";
    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("ZlgEtherCat", () => new WriteAnalogOutputExecutor());
    public override string GenerateDescription(byte[] setting)
    {
        try
        {
            var s = (WriteAnalogOutputSetting)CreateSerializer().Deserialize(setting, 1);
            return s.Operation switch
            {
                WriteAnalogOutputOperation.WriteAnalogOutput => $"写入模拟量输出：连接名 {s.ConnectionName}；从站 ENI 索引（常量） {(string.IsNullOrWhiteSpace(s.SlaveIndexExpression) ? s.SlaveIndex.ToString() : s.SlaveIndexExpression)}；从站输出区相对字节偏移 {s.Offset}；数据类型 {s.DataType}；工程值 {(string.IsNullOrWhiteSpace(s.ValueExpression) ? s.Value.ToString() : s.ValueExpression)}；结果变量 {s.ResultVariable}",
                _ => "写入模拟量输出（所选功能未定义）"
            };
        }
        catch (Exception ex) { return "写入模拟量输出（设置无法解析：" + ex.Message + "）"; }
    }
    public override string Description => """
        ## 功能
        写入模拟量输出。本步骤为固定功能，只执行 WriteAnalogOutput，无需选择操作；编辑参数不会执行设备动作。

        ## 参数
        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        | --- | --- | --- | --- | --- |
        | Operation | WriteAnalogOutputOperation | 固定值，无需选择 | WriteAnalogOutput | WriteAnalogOutput=0（写入模拟量输出）；固定功能，不接受其他操作 |
        | ConnectionName | string([ExpressionField]) | 是 | "ECAT1" | WriteAnalogOutput；连接名；求值结果为 string；选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名 |
        | SlaveIndex | int | 是 | 0 | WriteAnalogOutput；从站 ENI 索引（常量）；本连接加载 ENI 中从 0 开始的 SlaveIdx；不是接线 PhysicalPosition、站地址、别名或同型号序号。先用 EnumerateSlaves 核对；SlaveIndexExpression 非空时此常量不生效。；范围 0～65534 |
        | SlaveIndexExpression | string([ExpressionField]) | 否 |  | WriteAnalogOutput；从站 ENI 索引表达式（可选）；求值结果为 int；留空使用上方从站常量；非空时优先求值。结果为本连接 ENI 中从 0 开始的 SlaveIdx，不是物理站位、站地址或别名。范围 0～65534。；范围 0～65534 |
        | Offset | int | 是 | 0 | WriteAnalogOutput；从站输出区相对字节偏移；相对指定从站输出映像起点，从 0 开始。不是全局 Offset，也不是对象索引；实际绝对位置=ReadPiRange 中输出 Offset+此值。；范围 0～16777215 |
        | DataType | PdoDataType | 是 | Int16 | WriteAnalogOutput；数据类型；可选值：Bytes — 原始 byte[]，长度按配置，不转换；Boolean — bool，1 字节；任一位非零为 true，不是单个位；UInt8 — byte，无符号 8 位，1 字节；Int8 — sbyte，有符号 8 位，1 字节；UInt16 — ushort，无符号 16 位，2 字节；Int16 — short，有符号 16 位，2 字节；UInt32 — uint，无符号 32 位，4 字节；Int32 — int，有符号 32 位，4 字节；UInt64 — ulong，无符号 64 位，8 字节；Int64 — long，有符号 64 位，8 字节；Float32 — float，IEEE 754，4 字节；Float64 — double，IEEE 754，8 字节。多字节固定小端；没有自动缩放。单个位请用位读取/数字输入步骤。 ENI 提供字段元数据时会核对标量类型、宽度与边界；Bytes 是原始访问，不核对对象类型。非字节对齐字段须用位步骤，不能选普通标量。 |
        | Value | double | 是 | 0 | WriteAnalogOutput；工程值；兼容常量；ValueExpression 非空时使用表达式结果。工程值按 Scale/Bias 反换算成指定数值类型；请核对整数舍入和量程。；范围 -1000000000000～1000000000000 |
        | ValueExpression | string([ExpressionField]) | 否 |  | WriteAnalogOutput；工程值表达式（可选）；求值结果为 double；留空使用上方常量；非空时用此表达式的结果作为本次输出，避免改写步骤配置。；范围 -1000000000000～1000000000000 |
        | Scale | double | 是 | 1 | WriteAnalogOutput；比例；有限且非零。读取：工程值=原始值×Scale+Bias；写入：原始值=(工程值-Bias)/Scale。单位由设备量程和用户约定，不自动识别；范围 -1000000000000～1000000000000 |
        | Bias | double | 是 | 0 | WriteAnalogOutput；偏移；工程单位零点偏移；读取在缩放后相加，写入在反缩放前相减；不是 PDO 字节偏移；范围 -1000000000000～1000000000000 |
        | TimeoutMs | int | 是 | 5000 | WriteAnalogOutput；步骤总超时（ms）；包括排队、表达式和执行等待；超时不证明原生调用已退出；范围 1～3600000 |
        | ResultVariable | string(VariablePathField) | 否 |  | WriteAnalogOutput；结果变量；可选已声明变量完整路径；bool：true 表示本次配置或软件操作完成，不证明物理动作完成。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。 |

        ## 行为
        - 平台助手直接读写本步骤参数；Operation 为唯一固定值，无需选择。全部参数公开可读写、保存，不需要 UI 私有状态或 JSON 脚本。
        - 本步骤只执行其固定功能的参数校验、表达式求值及设备调用；打开、关闭、启动、停止分别由各自独立步骤执行。
        - 当前平台宿主可能预编译表达式字段；预编译不求值、不触发设备动作。此步骤只包含其固定操作的参数。
        - ResultVariable 是已声明变量路径，保存原始返回值；步骤结果显示 JSON。TimeoutMs 为本步骤配置总期限。
        - 超时和失败返回带中文 ErrorInfo 的 Error；仅原调用方取消返回 Aborted。SDK 提交完成不证明设备已停止。
        ### WriteAnalogOutput — 写入模拟量输出
        - 适用参数：ConnectionName、TimeoutMs、SlaveIndex、SlaveIndexExpression、Offset、DataType、Value、ValueExpression、Scale、Bias、ResultVariable。

        - 目标与范围：目标为 ConnectionName + ENI 零基 SlaveIndex 选择的单个从站；同型号模块按不同 ENI 索引分别配置。
        - 前置操作：ZlgEtherCat.EnablePi；编辑器只提示静态顺序，执行时仍检查真实状态。
        - 返回值：bool：true 表示本次配置或软件操作完成，不证明物理动作完成。
        -  执行时必须已确认 OP；StartBus 的 RequireDc=true 时还须确认 DC 内部同步与传播延迟初始化。 任一轴绑定了目标从站时，拒绝该从站的全部原始输出写入；其他从站的输入读取不受此门禁限制。 BeginExit 后拒绝本操作；完成关闭并重新打开连接后才可开展新的业务。

        ## 相关插件
        ZlgEtherCat.EnablePi、ZlgEtherCat.Open。
        """;
}
