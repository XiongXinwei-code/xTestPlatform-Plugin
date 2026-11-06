using ZlgEtherCat.Executors;
using ZlgEtherCat.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace ZlgEtherCat.Plugins;

public sealed class ReadSdoPlugin : ZlgEtherCat.PluginInfrastructure.StrictStepPluginBase<ReadSdoSetting>
{
    public override string StepTypeId => "ZlgEtherCat.ReadSdo";
    public override string DisplayName => "ZlgEtherCat_Parameters_ReadSdo";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/ZlgEtherCat.StepPlugin.UI;component/Resources/Icons/zlg_readsdo.png";
    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("ZlgEtherCat", () => new ReadSdoExecutor());
    public override string GenerateDescription(byte[] setting)
    {
        try
        {
            var s = (ReadSdoSetting)CreateSerializer().Deserialize(setting, 1);
            return s.Operation switch
            {
                ReadSdoOperation.ReadSdo => $"读取 SDO 参数：连接名 {s.ConnectionName}；从站 ENI 索引（常量） {(string.IsNullOrWhiteSpace(s.SlaveIndexExpression) ? s.SlaveIndex.ToString() : s.SlaveIndexExpression)}；CoE 对象索引（常量） {s.Index}；子索引 {s.SubIndex}；数据类型 {s.DataType}；结果变量 {s.ResultVariable}",
                _ => "读取 SDO 参数（所选功能未定义）"
            };
        }
        catch (Exception ex) { return "读取 SDO 参数（设置无法解析：" + ex.Message + "）"; }
    }
    public override string Description => """
        ## 功能
        读取 SDO 参数。本步骤为固定功能，只执行 ReadSdo，无需选择操作；编辑参数不会执行设备动作。

        ## 参数
        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        | --- | --- | --- | --- | --- |
        | Operation | ReadSdoOperation | 固定值，无需选择 | ReadSdo | ReadSdo=0（读取 SDO 参数）；固定功能，不接受其他操作 |
        | ConnectionName | string([ExpressionField]) | 是 | "ECAT1" | ReadSdo；连接名；求值结果为 string；选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名 |
        | SlaveIndex | int | 是 | 0 | ReadSdo；从站 ENI 索引（常量）；本连接加载 ENI 中从 0 开始的 SlaveIdx；不是接线 PhysicalPosition、站地址、别名或同型号序号。先用 EnumerateSlaves 核对；SlaveIndexExpression 非空时此常量不生效。；范围 0～65534 |
        | SlaveIndexExpression | string([ExpressionField]) | 否 |  | ReadSdo；从站 ENI 索引表达式（可选）；求值结果为 int；留空使用上方从站常量；非空时优先求值。结果为本连接 ENI 中从 0 开始的 SlaveIdx，不是物理站位、站地址或别名。范围 0～65534。；范围 0～65534 |
        | Index | int | 是 | 4096 | ReadSdo；CoE 对象索引（常量）；当前从站对象字典索引，范围 0～65535；支持 0x6040 或十进制 24640。不能填 PDO 字节偏移或从站编号。；范围 0～65535 |
        | SubIndex | int | 是 | 0 | ReadSdo；子索引；同一对象内的子索引，0～255；不是从站索引。CompleteAccess=true 时仅允许 0 或 1，具体起点由设备对象字典确定；范围 0～255 |
        | CompleteAccess | bool | 是 | false | ReadSdo；完全访问；false 只访问一个 Index:SubIndex；true 是同一对象内多个子索引的整体访问，不是读取所有从站，当前仅支持 DataType=Bytes；需设备支持 |
        | Capacity | int | 是 | 4 | ReadSdo；接收缓冲容量；最大接收字节容量，可以大于标量宽度；标量模式容量不得小于类型宽度，并要求实际返回长度与类型一致；CompleteAccess 应选 Bytes 并提供完整块容量；范围 1～1048576 |
        | DataType | PdoDataType | 是 | Bytes | ReadSdo；数据类型；可选值：Bytes — 原始 byte[]，长度按配置，不转换；Boolean — bool，1 字节；任一位非零为 true，不是单个位；UInt8 — byte，无符号 8 位，1 字节；Int8 — sbyte，有符号 8 位，1 字节；UInt16 — ushort，无符号 16 位，2 字节；Int16 — short，有符号 16 位，2 字节；UInt32 — uint，无符号 32 位，4 字节；Int32 — int，有符号 32 位，4 字节；UInt64 — ulong，无符号 64 位，8 字节；Int64 — long，有符号 64 位，8 字节；Float32 — float，IEEE 754，4 字节；Float64 — double，IEEE 754，8 字节。多字节固定小端；没有自动缩放。单个位请用位读取/数字输入步骤。 |
        | TimeoutMs | int | 是 | 5000 | ReadSdo；步骤总超时（ms）；包括排队、表达式和执行等待；超时不证明原生调用已退出；范围 1～3600000 |
        | ResultVariable | string(VariablePathField) | 否 |  | ReadSdo；结果变量；可选已声明变量完整路径；随 DataType 返回 byte[]、bool、byte/sbyte、ushort/short、uint/int、ulong/long 或 float/double；多字节固定小端；Bytes 时返回实际收到的完整字节块。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。 |

        ## 行为
        - 平台助手直接读写本步骤参数；Operation 为唯一固定值，无需选择。全部参数公开可读写、保存，不需要 UI 私有状态或 JSON 脚本。
        - 本步骤只执行其固定功能的参数校验、表达式求值及设备调用；打开、关闭、启动、停止分别由各自独立步骤执行。
        - 当前平台宿主可能预编译表达式字段；预编译不求值、不触发设备动作。此步骤只包含其固定操作的参数。
        - ResultVariable 是已声明变量路径，保存原始返回值；步骤结果显示 JSON。TimeoutMs 为本步骤配置总期限。
        - 超时和失败返回带中文 ErrorInfo 的 Error；仅原调用方取消返回 Aborted。SDK 提交完成不证明设备已停止。
        ### ReadSdo — 读取 SDO 参数
        - 适用参数：ConnectionName、TimeoutMs、SlaveIndex、SlaveIndexExpression、Index、SubIndex、CompleteAccess、Capacity、DataType、ResultVariable。

        - 目标与范围：目标为 ConnectionName + ENI 零基 SlaveIndex 选择的单个从站；同型号模块按不同 ENI 索引分别配置。
        - 前置操作：ZlgEtherCat.StartBus；编辑器只提示静态顺序，执行时仍检查真实状态。
        - 返回值：随 DataType 返回 byte[]、bool、byte/sbyte、ushort/short、uint/int、ulong/long 或 float/double；多字节固定小端；Bytes 时返回实际收到的完整字节块。
        -  Queue 周期输出运行期间拒绝本操作，避免长邮箱/维护调用阻塞唯一 SDK 线程；需先完成停轴、去使能、解绑并停止周期输出。

        ## 相关插件
        ZlgEtherCat.StartBus、ZlgEtherCat.Open。
        """;
}
