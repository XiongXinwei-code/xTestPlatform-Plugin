using ZlgEtherCat.Executors;
using ZlgEtherCat.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace ZlgEtherCat.Plugins;

public sealed class WriteSlaveMemoryPlugin : ZlgEtherCat.PluginInfrastructure.StrictStepPluginBase<WriteSlaveMemorySetting>
{
    public override string StepTypeId => "ZlgEtherCat.WriteSlaveMemory";
    public override string DisplayName => "ZlgEtherCat_Maintenance_WriteSlaveMemory";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/ZlgEtherCat.StepPlugin.UI;component/Resources/Icons/zlg_writeslavememory.png";
    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("ZlgEtherCat", () => new WriteSlaveMemoryExecutor());
    public override string GenerateDescription(byte[] setting)
    {
        try
        {
            var s = (WriteSlaveMemorySetting)CreateSerializer().Deserialize(setting, 1);
            return s.Operation switch
            {
                WriteSlaveMemoryOperation.WriteSlaveMemory => $"写入 ESC 内存：连接名 {s.ConnectionName}；从站 ENI 索引（常量） {(string.IsNullOrWhiteSpace(s.SlaveIndexExpression) ? s.SlaveIndex.ToString() : s.SlaveIndexExpression)}；结果变量 {s.ResultVariable}",
                _ => "写入 ESC 内存（所选功能未定义）"
            };
        }
        catch (Exception ex) { return "写入 ESC 内存（设置无法解析：" + ex.Message + "）"; }
    }
    public override string Description => """
        ## 功能
        写入 ESC 内存。本步骤为固定功能，只执行 WriteSlaveMemory，无需选择操作；编辑参数不会执行设备动作。

        ## 参数
        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        | --- | --- | --- | --- | --- |
        | Operation | WriteSlaveMemoryOperation | 固定值，无需选择 | WriteSlaveMemory | WriteSlaveMemory=0（写入 ESC 内存）；固定功能，不接受其他操作 |
        | ConnectionName | string([ExpressionField]) | 是 | "ECAT1" | WriteSlaveMemory；连接名；求值结果为 string；选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名 |
        | SlaveIndex | int | 是 | 0 | WriteSlaveMemory；从站 ENI 索引（常量）；本连接加载 ENI 中从 0 开始的 SlaveIdx；不是接线 PhysicalPosition、站地址、别名或同型号序号。先用 EnumerateSlaves 核对；SlaveIndexExpression 非空时此常量不生效。；范围 0～65534 |
        | SlaveIndexExpression | string([ExpressionField]) | 否 |  | WriteSlaveMemory；从站 ENI 索引表达式（可选）；求值结果为 int；留空使用上方从站常量；非空时优先求值。结果为本连接 ENI 中从 0 开始的 SlaveIdx，不是物理站位、站地址或别名。范围 0～65534。；范围 0～65534 |
        | Address | int | 是 | 0 | WriteSlaveMemory；ESC 地址；ESC 寄存器起始字节地址，支持十进制/0x；不是 PDO 映像偏移或 CoE 对象索引；范围 0～65535 |
        | Data | string([ExpressionField]) | 是 | new byte[] { 0 } | WriteSlaveMemory；写入数据；求值结果为 byte[]；求值结果为 byte[]；多字节按小端编码 |
        | TimeoutMs | int | 是 | 5000 | WriteSlaveMemory；步骤总超时（ms）；包括排队、表达式和执行等待；超时不证明原生调用已退出；范围 1～3600000 |
        | ResultVariable | string(VariablePathField) | 否 |  | WriteSlaveMemory；结果变量；可选已声明变量完整路径；无业务返回值（null）；ResultVariable 通常留空，执行是否完成读取步骤状态，不把 null 当作成功标志。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。 |

        ## 行为
        - 平台助手直接读写本步骤参数；Operation 为唯一固定值，无需选择。全部参数公开可读写、保存，不需要 UI 私有状态或 JSON 脚本。
        - 本步骤只执行其固定功能的参数校验、表达式求值及设备调用；打开、关闭、启动、停止分别由各自独立步骤执行。
        - 当前平台宿主可能预编译表达式字段；预编译不求值、不触发设备动作。此步骤只包含其固定操作的参数。
        - ResultVariable 是已声明变量路径，保存原始返回值；步骤结果显示 JSON。TimeoutMs 为本步骤配置总期限。
        - 超时和失败返回带中文 ErrorInfo 的 Error；仅原调用方取消返回 Aborted。SDK 提交完成不证明设备已停止。
        ### WriteSlaveMemory — 写入 ESC 内存
        - 适用参数：ConnectionName、TimeoutMs、SlaveIndex、SlaveIndexExpression、Address、Data、ResultVariable。

        - 目标与范围：目标为 ConnectionName + ENI 零基 SlaveIndex 选择的单个从站；同型号模块按不同 ENI 索引分别配置。
        - 前置操作：ZlgEtherCat.Open；编辑器只提示静态顺序，执行时仍检查真实状态。
        - 返回值：无业务返回值（null）；ResultVariable 通常留空，执行是否完成读取步骤状态，不把 null 当作成功标志。
        - 仅停机维护使用，不自动恢复或重试。 要求总线已经启动且处于 PREOP 或 BOOT、过程映像交换禁用；不等于 StopBus 后使用。 本操作要求整个连接没有任何绑定轴；即使目标是其他从站，也须先停轴、去使能并解绑。 要求连接内无绑定轴、两类命名读取池均已关闭、采集和周期输出已停止、过程映像交换已禁用。 BeginExit 后拒绝本操作；完成关闭并重新打开连接后才可开展新的业务。

        ## 相关插件
        ZlgEtherCat.Open。
        """;
}
