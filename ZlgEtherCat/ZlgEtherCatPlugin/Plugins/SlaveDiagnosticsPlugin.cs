using ZlgEtherCat.Executors;
using ZlgEtherCat.Models;
using xTestPlatform.Core.Plugins.Contracts;

namespace ZlgEtherCat.Plugins;

public sealed class SlaveDiagnosticsPlugin : ZlgEtherCat.PluginInfrastructure.StrictStepPluginBase<SlaveDiagnosticsSetting>
{
    public override string StepTypeId => "ZlgEtherCat.SlaveDiagnostics";
    public override string DisplayName => "ZlgEtherCat_Diagnostics_SlaveDiagnostics";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/ZlgEtherCat.StepPlugin.UI;component/Resources/Icons/zlg_slavediagnostics.png";
    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("ZlgEtherCat", () => new SlaveDiagnosticsExecutor());
    public override string GenerateDescription(byte[] setting)
    {
        try
        {
            var s = (SlaveDiagnosticsSetting)CreateSerializer().Deserialize(setting, 1);
            return s.Operation switch
            {
                SlaveDiagnosticsOperation.ReadSlaveInfo => $"读取从站信息：连接名 {s.ConnectionName}；从站 ENI 索引（常量） {(string.IsNullOrWhiteSpace(s.SlaveIndexExpression) ? s.SlaveIndex.ToString() : s.SlaveIndexExpression)}；结果变量 {s.ResultVariable}",
                SlaveDiagnosticsOperation.ReadSlaveStatus => $"读取从站状态：连接名 {s.ConnectionName}；从站 ENI 索引（常量） {(string.IsNullOrWhiteSpace(s.SlaveIndexExpression) ? s.SlaveIndex.ToString() : s.SlaveIndexExpression)}；结果变量 {s.ResultVariable}",
                _ => "从站信息与状态读取（所选功能未定义）"
            };
        }
        catch (Exception ex) { return "从站信息与状态读取（设置无法解析：" + ex.Message + "）"; }
    }
    public override string Description => """
        ## 功能
        从站信息与状态读取。Operation 只在同类功能中选择且只执行一个操作；选择功能或编辑参数不会执行设备动作。

        ## 参数
        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        | --- | --- | --- | --- | --- |
        | Operation | SlaveDiagnosticsOperation | 是 | ReadSlaveInfo | ReadSlaveInfo=0（读取从站信息）；ReadSlaveStatus=1（读取从站状态） |
        | ConnectionName | string([ExpressionField]) | 仅适用操作必填 | "ECAT1" | ReadSlaveInfo、ReadSlaveStatus；连接名；求值结果为 string；选择已打开的本插件板卡通道连接；不是从站名称、网卡名称或轴名 |
        | SlaveIndex | int | 仅适用操作必填 | 0 | ReadSlaveInfo、ReadSlaveStatus；从站 ENI 索引（常量）；本连接加载 ENI 中从 0 开始的 SlaveIdx；不是接线 PhysicalPosition、站地址、别名或同型号序号。先用 EnumerateSlaves 核对；SlaveIndexExpression 非空时此常量不生效。；范围 0～65534 |
        | SlaveIndexExpression | string([ExpressionField]) | 否 |  | ReadSlaveInfo、ReadSlaveStatus；从站 ENI 索引表达式（可选）；求值结果为 int；留空使用上方从站常量；非空时优先求值。结果为本连接 ENI 中从 0 开始的 SlaveIdx，不是物理站位、站地址或别名。范围 0～65534。；范围 0～65534 |
        | TimeoutMs | int | 仅适用操作必填 | 5000 | ReadSlaveInfo、ReadSlaveStatus；步骤总超时（ms）；包括排队、表达式和执行等待；超时不证明原生调用已退出；范围 1～3600000 |
        | ResultVariable | string(VariablePathField) | 否 |  | ReadSlaveInfo、ReadSlaveStatus；结果变量；不同操作的范围、方向和返回类型见下方逐操作参数说明 |

        ## 行为
        - 平台助手先选择 Operation，再读写该操作的适用参数；全部参数公开可读写、保存，不需要 UI 私有状态或 JSON 脚本。
        - 只对所选操作执行参数校验、表达式求值及设备调用；未选参数保留，切换不自动执行任何操作。
        - 当前平台宿主可能预编译表达式字段；预编译不求值、不触发设备动作。未选字段的格式错误不影响本步骤所选操作的业务校验和求值。
        - ResultVariable 是已声明变量路径，保存原始返回值；步骤结果显示 JSON。TimeoutMs 为本步骤配置总期限。
        - 超时和失败返回带中文 ErrorInfo 的 Error；仅原调用方取消返回 Aborted。SDK 提交完成不证明设备已停止。
        ### ReadSlaveInfo — 读取从站信息
        - 适用参数：ConnectionName、TimeoutMs、SlaveIndex、SlaveIndexExpression、ResultVariable。
        - ResultVariable：可选已声明变量完整路径；对象：SlaveIndex、Active、Ready、FixedAddress、InfoSource、FixedAddressSource，以及可空的 VendorId、ProductCode、SerialNumber、Revision、Alias、InputBits、OutputBits（uint?）；离线时身份字段可为 null，型号不能唯一定位模块。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。
        - 目标与范围：目标为 ConnectionName + ENI 零基 SlaveIndex 选择的单个从站；同型号模块按不同 ENI 索引分别配置。
        - 前置操作：ZlgEtherCat.StartBus；编辑器只提示静态顺序，执行时仍检查真实状态。
        - 返回值：对象：SlaveIndex、Active、Ready、FixedAddress、InfoSource、FixedAddressSource，以及可空的 VendorId、ProductCode、SerialNumber、Revision、Alias、InputBits、OutputBits（uint?）；离线时身份字段可为 null，型号不能唯一定位模块。
        - 使用指定连接执行本步骤操作；连接或前置资源不存在时返回 Error。

        ### ReadSlaveStatus — 读取从站状态
        - 适用参数：ConnectionName、TimeoutMs、SlaveIndex、SlaveIndexExpression、ResultVariable。
        - ResultVariable：可选已声明变量完整路径；对象：SlaveIndex、Active、Ready、FixedAddress、FixedAddressSource、State（byte?）、AlStatus（ushort?）、DlStatus（ushort?）；离线时状态字段可为 null，不用 0 假装在线状态。复杂结果需声明可容纳对象/数组的变量；不填则仅步骤结果保留返回值。
        - 目标与范围：目标为 ConnectionName + ENI 零基 SlaveIndex 选择的单个从站；同型号模块按不同 ENI 索引分别配置。
        - 前置操作：ZlgEtherCat.StartBus；编辑器只提示静态顺序，执行时仍检查真实状态。
        - 返回值：对象：SlaveIndex、Active、Ready、FixedAddress、FixedAddressSource、State（byte?）、AlStatus（ushort?）、DlStatus（ushort?）；离线时状态字段可为 null，不用 0 假装在线状态。
        - 使用指定连接执行本步骤操作；连接或前置资源不存在时返回 Error。

        ## 相关插件
        ZlgEtherCat.StartBus、ZlgEtherCat.Open。
        """;
}
