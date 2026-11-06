using ZlgEtherCat.Executors;
using ZlgEtherCat.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace ZlgEtherCat.Plugins;

public sealed class OpenPlugin : ZlgEtherCat.PluginInfrastructure.StrictStepPluginBase<OpenSetting>
{
    public override string StepTypeId => "ZlgEtherCat.Open";
    public override string DisplayName => "ZlgEtherCat_Master_Open";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/ZlgEtherCat.StepPlugin.UI;component/Resources/Icons/zlg_open.png";
    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("ZlgEtherCat", () => new OpenExecutor());
    public override string GenerateDescription(byte[] setting)
    {
        try
        {
            var s = (OpenSetting)CreateSerializer().Deserialize(setting, 1);
            return s.Operation switch
            {
                OpenOperation.Open => $"打开板卡通道：连接名 {s.ConnectionName}；板卡标识 {s.BoardSelector}；通道号 {s.Channel}；结果变量 {s.ResultVariable}",
                _ => "打开板卡通道（所选功能未定义）"
            };
        }
        catch (Exception ex) { return "打开板卡通道（设置无法解析：" + ex.Message + "）"; }
    }
    public override string Description => """
        ## 功能
        打开板卡通道。本步骤为固定功能，只执行 Open，无需选择操作；编辑参数不会执行设备动作。

        ## 参数
        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        | --- | --- | --- | --- | --- |
        | Operation | OpenOperation | 固定值，无需选择 | Open | Open=0（打开板卡通道）；固定功能，不接受其他操作 |
        | ConnectionName | string([ExpressionField]) | 是 | "ECAT1" | Open；连接名；求值结果为 string；本插件连接会话名称表达式；用于后续步骤选择同一板卡通道，不是板卡序列号或从站编号 |
        | BoardSelector | string([ExpressionField]) | 是 | "AL1" | Open；板卡标识；求值结果为 string；求值结果为 string；SDK ID索引、AL别名或NO序列号 |
        | Channel | int | 是 | 0 | Open；通道号；；范围 0～255 |
        | TimeoutMs | int | 是 | 5000 | Open；步骤总超时（ms）；包括排队、表达式和执行等待；超时不证明原生调用已退出；范围 1～3600000 |
        | ResultVariable | string(VariablePathField) | 否 |  | Open；结果变量；可选已声明变量完整路径；已选板卡：Index 与 Info（Name、Version、SerialNumber、Alias、ChannelCount、FpgaVersion）。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。 |

        ## 行为
        - 平台助手直接读写本步骤参数；Operation 为唯一固定值，无需选择。全部参数公开可读写、保存，不需要 UI 私有状态或 JSON 脚本。
        - 本步骤只执行其固定功能的参数校验、表达式求值及设备调用；打开、关闭、启动、停止分别由各自独立步骤执行。
        - 当前平台宿主可能预编译表达式字段；预编译不求值、不触发设备动作。此步骤只包含其固定操作的参数。
        - ResultVariable 是已声明变量路径，保存原始返回值；步骤结果显示 JSON。TimeoutMs 为本步骤配置总期限。
        - 超时和失败返回带中文 ErrorInfo 的 Error；仅原调用方取消返回 Aborted。SDK 提交完成不证明设备已停止。
        ### Open — 打开板卡通道
        - 适用参数：ConnectionName、TimeoutMs、BoardSelector、Channel、ResultVariable。

        - 目标与范围：目标为 ConnectionName 对应的板卡通道；整板维护步骤会影响该板卡全部通道。
        - 前置操作：无；编辑器只提示静态顺序，执行时仍检查真实状态。
        - 返回值：已选板卡：Index 与 Info（Name、Version、SerialNumber、Alias、ChannelCount、FpgaVersion）。
        - 以 ConnectionName 注册连接；同名或同物理通道重复打开报错，不静默替换。

        ## 相关插件
        ZlgEtherCat.Open。
        """;
}
