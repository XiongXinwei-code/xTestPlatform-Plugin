using ZlgEtherCat.Executors;
using ZlgEtherCat.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace ZlgEtherCat.Plugins;

public sealed class ReadBoardPulsePlugin : ZlgEtherCat.PluginInfrastructure.StrictStepPluginBase<ReadBoardPulseSetting>
{
    public override string StepTypeId => "ZlgEtherCat.ReadBoardPulse";
    public override string DisplayName => "ZlgEtherCat_BoardIO_ReadBoardPulse";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/ZlgEtherCat.StepPlugin.UI;component/Resources/Icons/zlg_readboardpulse.png";
    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("ZlgEtherCat", () => new ReadBoardPulseExecutor());
    public override string GenerateDescription(byte[] setting)
    {
        try
        {
            var s = (ReadBoardPulseSetting)CreateSerializer().Deserialize(setting, 1);
            return s.Operation switch
            {
                ReadBoardPulseOperation.ReadBoardPulse => $"读取板载脉冲配置：连接名 {s.ConnectionName}；输出通道 {s.Port}；结果变量 {s.ResultVariable}",
                _ => "读取板载脉冲配置（所选功能未定义）"
            };
        }
        catch (Exception ex) { return "读取板载脉冲配置（设置无法解析：" + ex.Message + "）"; }
    }
    public override string Description => """
        ## 功能
        读取板载脉冲配置。本步骤为固定功能，只执行 ReadBoardPulse，无需选择操作；编辑参数不会执行设备动作。

        ## 参数
        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        | --- | --- | --- | --- | --- |
        | Operation | ReadBoardPulseOperation | 固定值，无需选择 | ReadBoardPulse | ReadBoardPulse=0（读取板载脉冲配置）；固定功能，不接受其他操作 |
        | ConnectionName | string([ExpressionField]) | 是 | "ECAT1" | ReadBoardPulse；连接名；求值结果为 string；选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名 |
        | Port | int | 是 | 0 | ReadBoardPulse；输出通道；；范围 0～3 |
        | TimeoutMs | int | 是 | 5000 | ReadBoardPulse；步骤总超时（ms）；包括排队、表达式和执行等待；超时不证明原生调用已退出；范围 1～3600000 |
        | ResultVariable | string(VariablePathField) | 否 |  | ReadBoardPulse；结果变量；可选已声明变量完整路径；对象：Mode、TriggerMode、Direction、Frequency（Hz）、DutyPercent（%）。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。 |

        ## 行为
        - 平台助手直接读写本步骤参数；Operation 为唯一固定值，无需选择。全部参数公开可读写、保存，不需要 UI 私有状态或 JSON 脚本。
        - 本步骤只执行其固定功能的参数校验、表达式求值及设备调用；打开、关闭、启动、停止分别由各自独立步骤执行。
        - 当前平台宿主可能预编译表达式字段；预编译不求值、不触发设备动作。此步骤只包含其固定操作的参数。
        - ResultVariable 是已声明变量路径，保存原始返回值；步骤结果显示 JSON。TimeoutMs 为本步骤配置总期限。
        - 超时和失败返回带中文 ErrorInfo 的 Error；仅原调用方取消返回 Aborted。SDK 提交完成不证明设备已停止。
        ### ReadBoardPulse — 读取板载脉冲配置
        - 适用参数：ConnectionName、TimeoutMs、Port、ResultVariable。

        - 目标与范围：目标为连接所属整块板卡的板载端口，不是 EtherCAT 从站 I/O。 当前仅开放已核实的 PCIe-1E16 / PCIe-1E16-SM 板载能力；HPCIe-2E 等其他型号不开放这些板载步骤。
        - 前置操作：ZlgEtherCat.Open；编辑器只提示静态顺序，执行时仍检查真实状态。
        - 返回值：对象：Mode、TriggerMode、Direction、Frequency（Hz）、DutyPercent（%）。
        - 使用指定连接执行本步骤操作；连接或前置资源不存在时返回 Error。
        - 板载端口仅按已核实的 PCIe-1E16 能力开放，其他型号不自动推断支持。

        ## 相关插件
        ZlgEtherCat.Open。
        """;
}
