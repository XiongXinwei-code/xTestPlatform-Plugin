using ZlgEtherCat.Executors;
using ZlgEtherCat.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace ZlgEtherCat.Plugins;

public sealed class PdoReadPlugin : ZlgEtherCat.PluginInfrastructure.StrictStepPluginBase<PdoReadSetting>
{
    public override string StepTypeId => "ZlgEtherCat.PdoRead";
    public override string DisplayName => "ZlgEtherCat_PDO_PdoRead";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/ZlgEtherCat.StepPlugin.UI;component/Resources/Icons/zlg_pdoread.png";
    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("ZlgEtherCat", () => new PdoReadExecutor());
    public override string GenerateDescription(byte[] setting)
    {
        try
        {
            var s = (PdoReadSetting)CreateSerializer().Deserialize(setting, 1);
            return s.Operation switch
            {
                PdoReadOperation.ReadPdo => $"读取 PDO 当前值：连接名 {s.ConnectionName}；从站 ENI 索引（常量） {(string.IsNullOrWhiteSpace(s.SlaveIndexExpression) ? s.SlaveIndex.ToString() : s.SlaveIndexExpression)}；从站输入区相对字节偏移 {s.Offset}；数据类型 {s.DataType}；结果变量 {s.ResultVariable}",
                PdoReadOperation.ReadPdoBits => $"按位读取 PDO：连接名 {s.ConnectionName}；从站 ENI 索引（常量） {(string.IsNullOrWhiteSpace(s.SlaveIndexExpression) ? s.SlaveIndex.ToString() : s.SlaveIndexExpression)}；从站输入区相对字节偏移 {s.Offset}；结果变量 {s.ResultVariable}",
                _ => "PDO 数据读取（所选功能未定义）"
            };
        }
        catch (Exception ex) { return "PDO 数据读取（设置无法解析：" + ex.Message + "）"; }
    }
    public override string Description => """
        ## 功能
        PDO 数据读取。Operation 只在同类功能中选择且只执行一个操作；选择功能或编辑参数不会执行设备动作。

        ## 参数
        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        | --- | --- | --- | --- | --- |
        | Operation | PdoReadOperation | 是 | ReadPdo | ReadPdo=0（读取 PDO 当前值）；ReadPdoBits=1（按位读取 PDO） |
        | ConnectionName | string([ExpressionField]) | 仅适用操作必填 | "ECAT1" | ReadPdo、ReadPdoBits；连接名；求值结果为 string；选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名 |
        | SlaveIndex | int | 仅适用操作必填 | 0 | ReadPdo、ReadPdoBits；从站 ENI 索引（常量）；本连接加载 ENI 中从 0 开始的 SlaveIdx；不是接线 PhysicalPosition、站地址、别名或同型号序号。先用 EnumerateSlaves 核对；SlaveIndexExpression 非空时此常量不生效。；范围 0～65534 |
        | SlaveIndexExpression | string([ExpressionField]) | 否 |  | ReadPdo、ReadPdoBits；从站 ENI 索引表达式（可选）；求值结果为 int；留空使用上方从站常量；非空时优先求值。结果为本连接 ENI 中从 0 开始的 SlaveIdx，不是物理站位、站地址或别名。范围 0～65534。；范围 0～65534 |
        | Offset | int | 仅适用操作必填 | 0 | ReadPdo、ReadPdoBits；从站输入区相对字节偏移；相对指定从站输入映像起点，从 0 开始。不是全局 Offset，也不是对象索引；实际绝对位置=ReadPiRange 中输入 Offset+此值。；范围 0～16777215 |
        | Length | int | 仅适用操作必填 | 1 | ReadPdo；字节长度；Bytes 时为原始字节数；标量必须为所选类型的固定宽度（1/2/4/8 字节），范围不得超出当前从站输入区；范围 1～1048576 |
        | DataType | PdoDataType | 仅适用操作必填 | Bytes | ReadPdo；数据类型；可选值：Bytes — 原始 byte[]，长度按配置，不转换；Boolean — bool，1 字节；任一位非零为 true，不是单个位；UInt8 — byte，无符号 8 位，1 字节；Int8 — sbyte，有符号 8 位，1 字节；UInt16 — ushort，无符号 16 位，2 字节；Int16 — short，有符号 16 位，2 字节；UInt32 — uint，无符号 32 位，4 字节；Int32 — int，有符号 32 位，4 字节；UInt64 — ulong，无符号 64 位，8 字节；Int64 — long，有符号 64 位，8 字节；Float32 — float，IEEE 754，4 字节；Float64 — double，IEEE 754，8 字节。多字节固定小端；没有自动缩放。单个位请用位读取/数字输入步骤。 ENI 提供字段元数据时会核对标量类型、宽度与边界；Bytes 是原始访问，不核对对象类型。非字节对齐字段须用位步骤，不能选普通标量。 |
        | MaxAgeMs | int | 仅适用操作必填 | 1000 | ReadPdo、ReadPdoBits；本地接收最大年龄（ms）；从主机读取完成的单调时钟时刻起算；不含数据此前在 SDK RX 队列中的等待时间，不能据此证明设备真实采样年龄。；范围 1～3600000 |
        | ReturnSample | bool | 仅适用操作必填 | false | ReadPdo；返回带序号的样本；false 保持旧行为仅返回解码值；true 返回 Value 与 Sequence 等样本元数据，结果变量应能接收对象，可将其 Sequence 用于下一次 AfterSequenceExpression |
        | BitOffset | int | 仅适用操作必填 | 0 | ReadPdoBits；字节内起始位；LSB0 位编号：0 为该字节最低位，7 为最高位；不等同于模块端子号，端子到位的对应关系由设备 PDO 映射确定；范围 0～7 |
        | BitLength | int | 仅适用操作必填 | 1 | ReadPdoBits；位数；可跨字节；从 BitOffset 起读取 1～64 位并返回 ulong。符号扩展和工程单位换算由后续逻辑显式处理；范围 1～64 |
        | TimeoutMs | int | 仅适用操作必填 | 5000 | ReadPdo、ReadPdoBits；步骤总超时（ms）；包括排队、表达式和执行等待；超时不证明原生调用已退出；范围 1～3600000 |
        | ResultVariable | string(VariablePathField) | 否 |  | ReadPdo、ReadPdoBits；结果变量；不同操作的范围、方向和返回类型见下方逐操作参数说明 |

        ## 行为
        - 平台助手先选择 Operation，再读写该操作的适用参数；全部参数公开可读写、保存，不需要 UI 私有状态或 JSON 脚本。
        - 只对所选操作执行参数校验、表达式求值及设备调用；未选参数保留，切换不自动执行任何操作。
        - 当前平台宿主可能预编译表达式字段；预编译不求值、不触发设备动作。未选字段的格式错误不影响本步骤所选操作的业务校验和求值。
        - ResultVariable 是已声明变量路径，保存原始返回值；步骤结果显示 JSON。TimeoutMs 为本步骤配置总期限。
        - 超时和失败返回带中文 ErrorInfo 的 Error；仅原调用方取消返回 Aborted。SDK 提交完成不证明设备已停止。
        ### ReadPdo — 读取 PDO 当前值
        - 适用参数：ConnectionName、TimeoutMs、SlaveIndex、SlaveIndexExpression、Offset、Length、DataType、MaxAgeMs、ReturnSample、ResultVariable。
        - ResultVariable：可选已声明变量完整路径；随 DataType 返回 byte[]、bool、byte/sbyte、ushort/short、uint/int、ulong/long 或 float/double；多字节固定小端；ReturnSample=true 时返回 Value、Sequence、SlaveIndex、Offset、Length、DataType、ReceivedTimestamp、LocalAgeMs，以及 TimestampSource="HostMonotonicReadCompletion"、TimestampFrequencyHz=Stopwatch.Frequency、HardwareTimestampAvailable=false、HardwareTimestamp=null；ReceivedTimestamp 是主机读取完成的单调时钟刻度，不是设备采样时刻，LocalAgeMs 不包含此前 RX 排队时间。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。
        - 目标与范围：目标为 ConnectionName + ENI 零基 SlaveIndex 选择的单个从站；同型号模块按不同 ENI 索引分别配置。
        - 前置操作：ZlgEtherCat.StartAcquisition；编辑器只提示静态顺序，执行时仍检查真实状态。
        - 返回值：随 DataType 返回 byte[]、bool、byte/sbyte、ushort/short、uint/int、ulong/long 或 float/double；多字节固定小端；ReturnSample=true 时返回 Value、Sequence、SlaveIndex、Offset、Length、DataType、ReceivedTimestamp、LocalAgeMs，以及 TimestampSource="HostMonotonicReadCompletion"、TimestampFrequencyHz=Stopwatch.Frequency、HardwareTimestampAvailable=false、HardwareTimestamp=null；ReceivedTimestamp 是主机读取完成的单调时钟刻度，不是设备采样时刻，LocalAgeMs 不包含此前 RX 排队时间。
        - 读取完整快照，不直接竞争 SDK 输入队列。

        ### ReadPdoBits — 按位读取 PDO
        - 适用参数：ConnectionName、TimeoutMs、SlaveIndex、SlaveIndexExpression、Offset、BitOffset、BitLength、MaxAgeMs、ResultVariable。
        - ResultVariable：可选已声明变量完整路径；ulong：按 LSB0 从低位起打包的无符号位域；BitLength=1 仍是 ulong（0/1）。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。
        - 目标与范围：目标为 ConnectionName + ENI 零基 SlaveIndex 选择的单个从站；同型号模块按不同 ENI 索引分别配置。
        - 前置操作：ZlgEtherCat.StartAcquisition；编辑器只提示静态顺序，执行时仍检查真实状态。
        - 返回值：ulong：按 LSB0 从低位起打包的无符号位域；BitLength=1 仍是 ulong（0/1）。
        - 使用指定连接执行本步骤操作；连接或前置资源不存在时返回 Error。

        ## 相关插件
        ZlgEtherCat.StartAcquisition、ZlgEtherCat.Open。
        """;
}
