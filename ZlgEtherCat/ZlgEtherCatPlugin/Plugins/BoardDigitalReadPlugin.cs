using ZlgEtherCat.Executors;
using ZlgEtherCat.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace ZlgEtherCat.Plugins;

public sealed class BoardDigitalReadPlugin : ZlgEtherCat.PluginInfrastructure.StrictStepPluginBase<BoardDigitalReadSetting>
{
    public override string StepTypeId => "ZlgEtherCat.BoardDigitalRead";
    public override string DisplayName => "ZlgEtherCat_BoardIO_BoardDigitalRead";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/ZlgEtherCat.StepPlugin.UI;component/Resources/Icons/zlg_boarddigitalread.png";
    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("ZlgEtherCat", () => new BoardDigitalReadExecutor());
    public override string GenerateDescription(byte[] setting)
    {
        try
        {
            var s = (BoardDigitalReadSetting)CreateSerializer().Deserialize(setting, 1);
            return s.Operation switch
            {
                BoardDigitalReadOperation.ReadBoardDi => $"读取板载 DI：连接名 {s.ConnectionName}；结果变量 {s.ResultVariable}",
                BoardDigitalReadOperation.ReadBoardDo => $"读取板载 DO：连接名 {s.ConnectionName}；结果变量 {s.ResultVariable}",
                _ => "板载数字量读取（所选功能未定义）"
            };
        }
        catch (Exception ex) { return "板载数字量读取（设置无法解析：" + ex.Message + "）"; }
    }
    public override string Description => """
        ## 功能
        板载数字量读取。Operation 只在同类功能中选择且只执行一个操作；选择功能或编辑参数不会执行设备动作。

        ## 参数
        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        | --- | --- | --- | --- | --- |
        | Operation | BoardDigitalReadOperation | 是 | ReadBoardDi | ReadBoardDi=0（读取板载 DI）；ReadBoardDo=1（读取板载 DO） |
        | ConnectionName | string([ExpressionField]) | 仅适用操作必填 | "ECAT1" | ReadBoardDi、ReadBoardDo；连接名；求值结果为 string；选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名 |
        | TimeoutMs | int | 仅适用操作必填 | 5000 | ReadBoardDi、ReadBoardDo；步骤总超时（ms）；包括排队、表达式和执行等待；超时不证明原生调用已退出；范围 1～3600000 |
        | ResultVariable | string(VariablePathField) | 否 |  | ReadBoardDi、ReadBoardDo；结果变量；不同操作的范围、方向和返回类型见下方逐操作参数说明 |

        ## 行为
        - 平台助手先选择 Operation，再读写该操作的适用参数；全部参数公开可读写、保存，不需要 UI 私有状态或 JSON 脚本。
        - 只对所选操作执行参数校验、表达式求值及设备调用；未选参数保留，切换不自动执行任何操作。
        - 当前平台宿主可能预编译表达式字段；预编译不求值、不触发设备动作。未选字段的格式错误不影响本步骤所选操作的业务校验和求值。
        - ResultVariable 是已声明变量路径，保存原始返回值；步骤结果显示 JSON。TimeoutMs 为本步骤配置总期限。
        - 超时和失败返回带中文 ErrorInfo 的 Error；仅原调用方取消返回 Aborted。SDK 提交完成不证明设备已停止。
        ### ReadBoardDi — 读取板载 DI
        - 适用参数：ConnectionName、TimeoutMs、ResultVariable。
        - ResultVariable：可选已声明变量完整路径；uint：板载 DI 位图，位对应板载端口而非 EtherCAT 从站模块。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。
        - 目标与范围：目标为连接所属整块板卡的板载端口，不是 EtherCAT 从站 I/O。 当前仅开放已核实的 PCIe-1E16 / PCIe-1E16-SM 板载能力；HPCIe-2E 等其他型号不开放这些板载步骤。
        - 前置操作：ZlgEtherCat.Open；编辑器只提示静态顺序，执行时仍检查真实状态。
        - 返回值：uint：板载 DI 位图，位对应板载端口而非 EtherCAT 从站模块。
        - 使用指定连接执行本步骤操作；连接或前置资源不存在时返回 Error。
        - 板载端口仅按已核实的 PCIe-1E16 能力开放，其他型号不自动推断支持。

        ### ReadBoardDo — 读取板载 DO
        - 适用参数：ConnectionName、TimeoutMs、ResultVariable。
        - ResultVariable：可选已声明变量完整路径；uint：板载 DO 位图，位对应板载端口而非 EtherCAT 从站模块。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。
        - 目标与范围：目标为连接所属整块板卡的板载端口，不是 EtherCAT 从站 I/O。 当前仅开放已核实的 PCIe-1E16 / PCIe-1E16-SM 板载能力；HPCIe-2E 等其他型号不开放这些板载步骤。
        - 前置操作：ZlgEtherCat.Open；编辑器只提示静态顺序，执行时仍检查真实状态。
        - 返回值：uint：板载 DO 位图，位对应板载端口而非 EtherCAT 从站模块。
        - 使用指定连接执行本步骤操作；连接或前置资源不存在时返回 Error。
        - 板载端口仅按已核实的 PCIe-1E16 能力开放，其他型号不自动推断支持。

        ## 相关插件
        ZlgEtherCat.Open。
        """;
}
