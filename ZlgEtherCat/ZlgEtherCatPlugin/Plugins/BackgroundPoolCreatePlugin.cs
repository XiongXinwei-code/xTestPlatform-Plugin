using ZlgEtherCat.Executors;
using ZlgEtherCat.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace ZlgEtherCat.Plugins;

public sealed class BackgroundPoolCreatePlugin : ZlgEtherCat.PluginInfrastructure.StrictStepPluginBase<BackgroundPoolCreateSetting>
{
    public override string StepTypeId => "ZlgEtherCat.BackgroundPoolCreate";
    public override string DisplayName => "ZlgEtherCat_Reading_BackgroundPoolCreate";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/ZlgEtherCat.StepPlugin.UI;component/Resources/Icons/zlg_backgroundpoolcreate.png";
    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("ZlgEtherCat", () => new BackgroundPoolCreateExecutor());
    public override string GenerateDescription(byte[] setting)
    {
        try
        {
            var s = (BackgroundPoolCreateSetting)CreateSerializer().Deserialize(setting, 1);
            return s.Operation switch
            {
                BackgroundPoolCreateOperation.BackgroundPoolCreate => $"创建常驻读取池：连接名 {s.ConnectionName}；功能池名 {s.PoolName}；结果变量 {s.ResultVariable}",
                _ => "创建常驻读取池（所选功能未定义）"
            };
        }
        catch (Exception ex) { return "创建常驻读取池（设置无法解析：" + ex.Message + "）"; }
    }
    public override string Description => """
        ## 功能
        创建常驻读取池。本步骤为固定功能，只执行 BackgroundPoolCreate，无需选择操作；编辑参数不会执行设备动作。

        ## 参数
        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        | --- | --- | --- | --- | --- |
        | Operation | BackgroundPoolCreateOperation | 固定值，无需选择 | BackgroundPoolCreate | BackgroundPoolCreate=0（创建常驻读取池）；固定功能，不接受其他操作 |
        | ConnectionName | string([ExpressionField]) | 是 | "ECAT1" | BackgroundPoolCreate；连接名；求值结果为 string；选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名 |
        | PoolName | string([ExpressionField]) | 是 | "ReadPool1" | BackgroundPoolCreate；功能池名；求值结果为 string；求值结果为 string；周期池与常驻池独立命名 |
        | MaxItems | int | 是 | 128 | BackgroundPoolCreate；读取项容量；每类读取功能池合计最多 1024 个读取项；范围 1～1024 |
        | TimeoutMs | int | 是 | 5000 | BackgroundPoolCreate；步骤总超时（ms）；包括排队、表达式和执行等待；超时不证明原生调用已退出；范围 1～3600000 |
        | ResultVariable | string(VariablePathField) | 否 |  | BackgroundPoolCreate；结果变量；可选已声明变量完整路径；bool：true 表示本次配置或软件操作完成，不证明物理动作完成。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。 |

        ## 行为
        - 平台助手直接读写本步骤参数；Operation 为唯一固定值，无需选择。全部参数公开可读写、保存，不需要 UI 私有状态或 JSON 脚本。
        - 本步骤只执行其固定功能的参数校验、表达式求值及设备调用；打开、关闭、启动、停止分别由各自独立步骤执行。
        - 当前平台宿主可能预编译表达式字段；预编译不求值、不触发设备动作。此步骤只包含其固定操作的参数。
        - ResultVariable 是已声明变量路径，保存原始返回值；步骤结果显示 JSON。TimeoutMs 为本步骤配置总期限。
        - 超时和失败返回带中文 ErrorInfo 的 Error；仅原调用方取消返回 Aborted。SDK 提交完成不证明设备已停止。
        ### BackgroundPoolCreate — 创建常驻读取池
        - 适用参数：ConnectionName、TimeoutMs、PoolName、MaxItems、ResultVariable。

        - 目标与范围：目标为指定连接、读取池类型及 PoolName；ItemName 只在该命名池内唯一。
        - 前置操作：ZlgEtherCat.StartAcquisition；编辑器只提示静态顺序，执行时仍检查真实状态。
        - 返回值：bool：true 表示本次配置或软件操作完成，不证明物理动作完成。
        - 以 PoolName 注册独立功能池；重名报错。固定工作循环不随读取项数量增长。 最新帧覆盖缓存可能合并中间帧；SkippedSamples 是读取项跳过的软件序号数，不是硬件丢帧统计。 BeginExit 后拒绝本操作；完成关闭并重新打开连接后才可开展新的业务。
        - 最新帧缓存可能合并中间帧；SkippedSamples 为软件样本跳过数，不是硬件丢帧。

        ## 相关插件
        ZlgEtherCat.StartAcquisition、ZlgEtherCat.Open。
        """;
}
