using ZlgEtherCat.Executors;
using ZlgEtherCat.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace ZlgEtherCat.Plugins;

public sealed class ConfigureExchangePlugin : ZlgEtherCat.PluginInfrastructure.StrictStepPluginBase<ConfigureExchangeSetting>
{
    public override string StepTypeId => "ZlgEtherCat.ConfigureExchange";
    public override string DisplayName => "ZlgEtherCat_Master_ConfigureExchange";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/ZlgEtherCat.StepPlugin.UI;component/Resources/Icons/zlg_configureexchange.png";
    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("ZlgEtherCat", () => new ConfigureExchangeExecutor());
    public override string GenerateDescription(byte[] setting)
    {
        try
        {
            var s = (ConfigureExchangeSetting)CreateSerializer().Deserialize(setting, 1);
            return s.Operation switch
            {
                ConfigureExchangeOperation.ConfigureExchange => $"配置 PDO 交换：连接名 {s.ConnectionName}；结果变量 {s.ResultVariable}",
                _ => "配置 PDO 交换（所选功能未定义）"
            };
        }
        catch (Exception ex) { return "配置 PDO 交换（设置无法解析：" + ex.Message + "）"; }
    }
    public override string Description => """
        ## 功能
        配置 PDO 交换。本步骤为固定功能，只执行 ConfigureExchange，无需选择操作；编辑参数不会执行设备动作。

        ## 参数
        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        | --- | --- | --- | --- | --- |
        | Operation | ConfigureExchangeOperation | 固定值，无需选择 | ConfigureExchange | ConfigureExchange=0（配置 PDO 交换）；固定功能，不接受其他操作 |
        | ConnectionName | string([ExpressionField]) | 是 | "ECAT1" | ConfigureExchange；连接名；求值结果为 string；选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名 |
        | ExchangeMode | ExchangeMode | 是 | Queue | ConfigureExchange；交换通路；可选值：Queue — SDK 输入/输出队列；运动 PDO 必须选此项；Direct — 直接过程映像访问，无新队列帧证明；Cycle — SDK 周期异步映像，无新队列帧证明；Manual — 显式更新异步映像，无新队列帧证明 |
        | QueuePrefill | int | 是 | 2 | ConfigureExchange；输出队列预填数量；仅 ExchangeMode=Queue 生效，填入完整初始输出映像的队列帧数；受真实 SDK 队列容量限制，不是毫秒；范围 0～4096 |
        | InitialOutput | string([ExpressionField]) | 是 | new byte[0] | ConfigureExchange；初始输出；求值结果为 byte[]；整个主站输出映像的 byte[] 初值，长度必须等于 ReadEniInfo.OutputBytes；不是单个从站输出，不能假定全零安全 |
        | TimeoutMs | int | 是 | 5000 | ConfigureExchange；步骤总超时（ms）；包括排队、表达式和执行等待；超时不证明原生调用已退出；范围 1～3600000 |
        | ResultVariable | string(VariablePathField) | 否 |  | ConfigureExchange；结果变量；可选已声明变量完整路径；对象：Mode（实际配置的交换模式）、OutputBytes（完整主站输出字节数）。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。 |

        ## 行为
        - 平台助手直接读写本步骤参数；Operation 为唯一固定值，无需选择。全部参数公开可读写、保存，不需要 UI 私有状态或 JSON 脚本。
        - 本步骤只执行其固定功能的参数校验、表达式求值及设备调用；打开、关闭、启动、停止分别由各自独立步骤执行。
        - 当前平台宿主可能预编译表达式字段；预编译不求值、不触发设备动作。此步骤只包含其固定操作的参数。
        - ResultVariable 是已声明变量路径，保存原始返回值；步骤结果显示 JSON。TimeoutMs 为本步骤配置总期限。
        - 超时和失败返回带中文 ErrorInfo 的 Error；仅原调用方取消返回 Aborted。SDK 提交完成不证明设备已停止。
        ### ConfigureExchange — 配置 PDO 交换
        - 适用参数：ConnectionName、TimeoutMs、ExchangeMode、QueuePrefill、InitialOutput、ResultVariable。

        - 目标与范围：目标为 ConnectionName 对应的板卡通道；整板维护步骤会影响该板卡全部通道。
        - 前置操作：ZlgEtherCat.StartBus；编辑器只提示静态顺序，执行时仍检查真实状态。
        - 返回值：对象：Mode（实际配置的交换模式）、OutputBytes（完整主站输出字节数）。
        - 运行中禁止切换通路；映像、队列与所有输出由单一通道运行时管理。 要求连接内无绑定轴、两类命名读取池均已关闭、采集和周期输出已停止、过程映像交换已禁用。 BeginExit 后拒绝本操作；完成关闭并重新打开连接后才可开展新的业务。

        ## 相关插件
        ZlgEtherCat.StartBus、ZlgEtherCat.Open。
        """;
}
