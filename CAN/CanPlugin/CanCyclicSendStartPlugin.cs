using CAN.Executors;
using CAN.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN;

public sealed class CanCyclicSendStartPlugin : CAN.PluginInfrastructure.StrictStepPluginBase<CanCyclicSendStartSetting>
{
    public override string StepTypeId => "IO.CanCyclicSendStart";
    public override string DisplayName => "CAN_Cyclic_SendStart";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can_cyclic_start.png";

    public override string Description => """
        ## 功能

        启动 CAN 周期发送任务，按配置的报文列表持续循环发送 CAN 帧，直到执行 CAN_Cyclic_SendStop 停止。用于模拟整车网络环境（如车速、转速等信号）。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | ConnectionName | string([ExpressionField]) | 是 | "CAN1" | 已打开的 CAN 连接名，求值结果为 string |
        | TaskName | string([ExpressionField]) | 是 | "CyclicTask1" | 任务标识名，Stop 时用此名称停止，求值结果为 string |
        | ControlRefreshMs | int | 否 | 20 | 动态启用/周期的检查间隔，必须大于0；受主机调度影响，不保证硬实时 |
        | ExpressionTimeoutMs | int | 否 | 5000 | 单次表达式求值期限，必须大于0；软等待结束后仍跟踪实际求值退出 |
        | Messages | 集合 | 是 | — | 周期报文列表，元素字段见下方 |

        Messages 元素字段：

        - CanId：string([ExpressionField])，CAN ID，求值结果为 string，支持 0x 前缀十六进制或十进制（如 "0x185"）
        - FrameType：枚举，帧类型，可选值：Standard, Extended
        - Data：string([ExpressionField])，发送数据，求值结果为十六进制 string，字节间可用空格、- 或逗号分隔（如 "FF FF FF FF FF FF FF FF"）
        - UseFdFrame：bool，默认 false；启用时以 CAN FD/BRS 发送，单行独立配置，要求 CAN_Open 使用 FD
        - CycleTimeMs：int，兼容旧序列的常量周期，默认100 ms
        - Enabled：bool，兼容旧序列的常量启用状态，默认true
        - CycleTimeMsExpression：string([ExpressionField])，默认空；非空时动态求值为大于0的int毫秒数，覆盖CycleTimeMs
        - EnabledExpression：string([ExpressionField])，默认空；非空时动态求值为bool，覆盖Enabled；false暂停，true恢复，不需要重建任务
        - Protection：对象，默认 RC/CRC 均关闭，各行独立配置，字段见下表

        编辑 RC/CRC 时可直接从保护区域的快捷下拉框选择报文，也可在上方列表选中行；两处指向同一个报文配置，不复制或覆盖其他行。
        下拉显示行号、原始 ID 表达式、Standard/Extended 和 Classic/CAN FD；相同 ID 可据此区分。编辑时不执行 ID 表达式。

        动态行为：启动时复制表达式文本与配置，运行时按 ControlRefreshMs 独立检查启用/周期。
        启动先校验一次 ID/Data；暂停运行期间不再求值 ID/Data，也不推进 Counter；恢复与周期改变重新安排下一次发送，不补发旧节拍，Counter保持连续。
        同 ID 配置覆盖保留下一 Counter；执行 Stop 后重新 Start 才从首帧 Counter 重启。已经进入驱动的提交无法撤回；观察到控制变化后，尚未提交的旧排队帧会被丢弃。
        表达式只应读取变量和计算，不应有设备操作、变量写入等副作用。非法结果或求值超时停止该周期任务并保留故障，不能使用旧值继续发送。
        初始false的动态行仍保留任务，之后可启用；静态false行保持旧版跳过行为。任务错误与实际求值退出分别跟踪，旧调用持续保留直到真实退出。

        Data ID 开关的完整路径是 Messages[].Protection.CrcDataIdEnabled，类型为 bool，使用 true / false；
        相应行的 Messages[].Protection.CrcEnabled 也须为 true。字段属于每行 Protection 对象，不属于步骤根层。
        Messages 是对象数组。工具仅支持整数组替换时，应先完整读回各行并保留未修改字段；不能用默认值补写未知行。
        读取若是逗号分隔的各项 JSON，写入时补外层 []；参数名使用 Messages。周期和启用状态编辑使用真实字段 CycleTimeMsExpression/EnabledExpression；不提供 CycleTimeText/EnabledText/FrameTypeInt UI 别名。按 schema 修改后读回核对。

        """ + CAN.Helpers.CanPayloadProtectionDescription.ParametersFor("Messages[].Protection") + """

        ## 行为

        - 步骤完整校验初始控制、ID、数据及表后更新连接发送池，不发送初始化帧，发送在后台持续进行
        - 每条启用的报文独立按各自 CycleTimeMs 周期发送，每次发送前重新求值 CanId 和 Data，可随变量变化实时更新；动态 ID 迁移使用目标 ID 槽位的节拍/RC，新 ID 从初值开始，遇到较新配置占用的 ID 则旧行停止，不能夺回
        - 每行 RC 独立，首次发送使用初值；Stop 确认退出后再次 Start 重置计数。RC/CRC 配置按启动时快照使用；动态 CAN ID 使用目标 ID 槽位的计数，首次出现的新 ID 从配置初值开始，返回未被新配置接管的旧 ID 时接续该 ID 的计数
        - 静态 Enabled=false 行兼容旧序列，整行跳过，不用于暂停已有 ID；暂停已有 ID 可使用 EnabledExpression=false。
        - 每行可独立选择经典帧或 CAN FD/BRS；经典帧最多 8 字节，FD 最多 64 字节。FD 非标准长度按适配器支持的 DLC 补齐
        - 发送为驱动提交，不等待设备响应或证明报文已上总线；FD 模式不自动将经典帧改成 FD
        - 所有行均为静态禁用时直接完成，不创建任务；动态行初始false时创建暂停任务，后续变量变化可启用
        - 单行表达式/组帧故障停止该行，其他健康 ID 继续；原生发送故障按原连接隔离规则处理，诊断保存在后台状态中
        - 连接名未找到时步骤报错
        - 任务注册到 ConnectionName 对应的连接资源内，以 TaskName 标识；不同连接的同名任务互不影响
        - 同一连接同帧类型同 ID（经典帧与 FD 共用）唯一；后启动配置替换该 ID，下一发送节拍生效，RC前后均启用时保留下一RC和已排节拍（新初值仅Stop后重启生效）。新RC范围无法表示下一计数时整次更新失败，旧配置保持不变。相同任务名只更新提交的 ID，未列出的旧 ID 保留；不同任务名接管后，旧任务 Stop 不会删除新归属 ID
        - 同连接由唯一发送池调度已就绪的到期报文，按 FIFO 批提交；慢表达式不会阻塞其他 ID 的准备。不保证 RPDO 与 SYNC 组合顺序，请使用专用 CANopen 有序服务

        """ + CAN.Helpers.CanPayloadProtectionDescription.Behavior + """

        ## 相关插件

        - `CAN_Open`：打开 CAN 通道
        - `CAN_Cyclic_SendStop`：停止本插件启动的周期发送任务
        """;

    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("CAN", () => new CanCyclicSendStartExecutor());

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        var rows = s.Messages.Where(m => m.Enabled || !string.IsNullOrWhiteSpace(m.EnabledExpression)).ToArray();
        var dynamicCount = rows.Count(m => !string.IsNullOrWhiteSpace(m.EnabledExpression) || !string.IsNullOrWhiteSpace(m.CycleTimeMsExpression));
        return $"CyclicSendStart {s.ConnectionName} Task={s.TaskName} ({rows.Length} messages, {rows.Count(m => m.UseFdFrame)} FD, {rows.Count(m => m.Protection?.RcEnabled == true || m.Protection?.CrcEnabled == true)} RC/CRC, {dynamicCount} expression rows)";
    }
}
