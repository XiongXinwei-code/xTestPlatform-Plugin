using CAN.Executors;
using CAN.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN;

public sealed class CanOpenPlugin : StepPluginBase<CanOpenSetting>
{
    public override string StepTypeId => "IO.CanOpen";
    public override string DisplayName => "CAN_Open";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can_open.png";

    public override string Description => """
        ## 功能

        打开 CAN 通道并建立连接，支持 CAN 2.0 Classic、CAN FD 协议。

        本插件按**协议**命名，不按厂商命名：所有厂商的 CAN 接口卡统一通过本插件接入，具体厂商由 `AdapterType` 字段选择。
        平台中不存在、也不需要按厂商或驱动命名的独立 CAN 插件（如 ZlgCan、PCAN、Kvaser 插件）——需要 CAN 通信时一律使用 `CAN_*` 系列步骤。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | AdapterType | 枚举 | 是 | — | 硬件类型，可选值：NI, PEAK, Vector, ZLG, Kvaser, TOSUN |
        | Channel | string([ExpressionField]) | 是 | — | 通道名称，求值结果为 string |
        | BaudRate | int | 是 | — | 仲裁段波特率 |
        | Protocol | 枚举 | 是 | Classic | 可选值：Classic, FD |
        | DataBitRate | int | FD 时 | 2000000 | 数据段波特率 |
        | EnableTermination | bool | 否 | false | 使能设备内置 120 Ω 终端电阻；需要硬件及厂商驱动支持 |
        | ArbitrationSamplePoint | double | 否 | 80.0 | 仲裁段目标采样点百分比，范围 7.5%~97.5%；插件在适配器内部转换为驱动位时序 |
        | DataSamplePoint | double | FD 时 | 80.0 | 数据段目标采样点百分比，范围 7.5%~97.5%；与仲裁段采样点独立配置 |
        | RxQueueSize | int | 否 | 8192 | 接收缓冲区大小（帧数）；NI-XNET 同时由后台接收泵持续排空驱动队列 |
        | ConnectionName | string([ExpressionField]) | 是 | — | 连接标识名，序列内唯一，求值结果为 string |
        | TimeoutMs | int | 否 | 5000 | 可配置打开总超时（ms，必须大于 0），包含登记排队、旧连接实际收尾与本次驱动打开；不是各阶段各等待一次 |

        ## 通道命名规则

        Channel 的格式取决于 AdapterType：

        - NI（NI-XNET）：接口名格式为 `CAN<编号>`，如 `CAN1`、`CAN2`，编号从 1 开始，在 NI MAX 的"设备和接口"中查看（多端口板卡每个端口一个接口名）
        - PEAK（PCAN-Basic）：通道名格式为 `PCAN_USBBUS<编号>`，如 `PCAN_USBBUS1`（也可简写为数字 `1`~`16`），在 PCAN-View 中查看
        - Vector（XL Driver Library）：全局通道索引，如 `0`、`1`，在 Vector Hardware Config 中查看
        - ZLG（周立功）：格式为 `<设备类型>/<设备索引>/<通道索引>`，如 `USBCAN2/0/0`、`USBCANFD-200U/0/1`、`PCI5010U/0/0`、`PCIe9120/0/1`、`PCIE-CANFD-200U/0/1`；设备索引、通道索引从 0 开始。支持现有 USB 型号和当前 SDK 全部 PCI / PCIe 型号，含 PCIe CAN FD 的 EX、MINI、M2、800U、1200U、MINI-PCIE-CANFD；型号不区分大小写，兼容 SDK 的 `ZCAN_*` 名称，完整列表见 ZLG 原生库 README
        - Kvaser（CANlib）：通道索引，如 `0`、`1`，在 Kvaser Hardware 工具中查看
        - TOSUN（同星）：通道索引，如 `0`、`1`，默认连接第一个 USB 设备

        注意：运行机器需安装对应厂商驱动（NI-XNET / PCAN-Basic / Vector XL Driver / ZLGCAN / Kvaser Drivers / TSMaster）。

        ## 行为

        - 打开的适配器会以 `ConnectionName` 为标识名注册到运行期资源表，供后续 CAN_Read / CAN_Write / CAN_Cyclic / CAN_Close / XCP 步骤取用
        - 活动连接禁止重复打开；若旧连接已经进入关闭，在 TimeoutMs 总预算内等待/重试其实际收尾，成功后才创建新连接。旧任务仍未退出或原生释放失败时报告具体阶段，不清空占用来伪装成功
        - 当前 TOSUN 接入采用单库所有者：同一插件实例库内仅允许一个 TOSUN 连接，不支持并行打开其不同通道或设备。后来的打开在 Init/Connect 前明确拒绝；旧 Disconnect/Finalize 未成功时继续保留所有权，须重试关闭。此限制避免单个连接关闭时全局 Finalize 影响其他连接，不表示已经实现共享设备管理
        - TOSUN 须由本插件独占当前进程的 libTSCAN 生命周期；Connect 返回已连接（5）或失败时提供归属未知句柄，将禁止 Disconnect/Finalize 并保持隔离。须关闭同进程其他使用者后重启宿主，不将外部连接强行接管或伪装为可立即重开
        - 硬件不存在或通道被占用时步骤报错
        - NI-XNET 的 FD+BRS 会话可混合发送经典 CAN 与 CAN FD：经典帧使用 CAN20_Data，FD 帧使用 CANFDBRS_Data；Classic 会话保持 CAN_Data
        - 界面只配置采样点百分比，不暴露厂商专用的 BRP/SJW/TSEG1/TSEG2；适配器内部根据设备时钟与驱动能力换算
        - 运行日志会分别输出 Nominal/Data 两段的目标采样点以及驱动实际采用的位时序；无法表达目标采样点时明确报错，不会静默忽略。PCI5010U / PCI5020U 为明确例外：使用驱动标准波特率，采样点由驱动决定，日志明确提示目标采样点未应用，不虚报实际采样点
        - 经典 CAN 仅使用仲裁段采样点；CAN FD 的仲裁段（Nominal Bit Rate）和数据段（Data Bit Rate）分别使用 ArbitrationSamplePoint 与 DataSamplePoint
        - NI、PEAK、Vector、Kvaser 支持分别按百分比换算仲裁段与 CAN FD 数据段；当前 libTSCAN 及 ZLG USB CAN FD 标准波特率接口使用 80% 仲裁段和 80% 数据段采样点。ZLG PCIe CAN FD 型号在 Classic / FD 下均使用 80% 仲裁段采样点；FD 的 5 Mbps 数据段使用 75%，其他 SDK 标准数据段使用 80%，超出 SDK 标准波特率范围时报错
        - PCI / PCIe 设备需要对应型号的 x64 二次开发板卡库和 Windows 驱动；仅存在 zlgcan.dll 或设备 XML 不代表依赖完整，打开失败时会提示板卡库缺失。PCIe9220 在当前 dll_cfg.ini 中没有映射，需使用包含该型号的官方 SDK
        - 软件终端电阻当前接入 NI-XNET、ZLGCAN 与 libTSCAN；PEAK、Vector、Kvaser 请使用外置 120 Ω 电阻
        - NI-XNET 接收会话启动后由后台接收泵持续抽取总线帧，UDS/普通读取按目标 ID 从内存队列路由取帧，未匹配 ID 的帧会保留给后续读取；后台泵异常或队列达到上限丢帧会立即记录诊断

        ## 检索关键词

        CAN、CAN bus、CAN 总线、CAN 2.0、CAN FD、CANFD、DBC、
        NI-XNET、XNET、PEAK、PCAN、PCAN-Basic、PCAN-USB、
        Vector、XL Driver Library、vxlapi、VN1610、
        ZLG、周立功、ZLGCAN、USBCAN、USBCANFD、PCI、PCIe、PCIE-CANFD、CANalyst、
        Kvaser、CANlib、TOSUN、同星、TSMaster

        ## 相关插件

        - `CAN_Write` / `CAN_Read`：在此连接上收发报文
        - `CAN_Cyclic_SendStart` / `CAN_Cyclic_SendStop`：周期发送
        - `CAN_Close`：关闭本插件打开的通道
        """;

    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("CAN", () => new CanOpenExecutor());

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        var proto = s.Protocol == CanProtocolType.FD ? "FD" : "Classic";
        var termination = s.EnableTermination ? ", 120Ω" : "";
        string samplePoint = s.Protocol == CanProtocolType.FD
            ? $"NominalSP={s.ArbitrationSamplePoint:F1}%, DataSP={s.DataSamplePoint:F1}%"
            : $"SP={s.ArbitrationSamplePoint:F1}%";
        return $"Open {s.ConnectionName} ({s.AdapterType}, {proto}, {s.BaudRate} bps, {samplePoint}{termination}, 总超时={s.TimeoutMs} ms)";
    }
}
