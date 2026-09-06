using CAN.Executors;
using CAN.Helpers;
using CAN.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN;

public sealed class CanOpenPlugin : StepPluginBase<CanOpenSetting>, IStepPlugin
{
    public override string StepTypeId => "IO.CanOpen";
    public override string DisplayName => "CAN_Open";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public override string Description => """
        ## 功能

        打开 CAN 通道并建立连接，支持 CAN 2.0 Classic、CAN FD 协议。

        本插件按**协议**命名，不按厂商命名：所有厂商的 CAN 接口卡统一通过本插件接入，具体厂商由 `AdapterType` 字段选择。
        平台中不存在、也不需要按厂商或驱动命名的独立 CAN 插件（如 ZlgCan、PCAN、Kvaser 插件）——需要 CAN 通信时一律使用 `CAN_*` 系列步骤。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | AdapterType | 枚举 | 是 | — | 硬件类型，可选值：NI, PEAK, Vector, ZLG, Kvaser, TOSUN |
        | Channel | string([ExpressionField]) | 是 | — | 通道名称 |
        | BaudRate | int | 是 | — | 仲裁段波特率 |
        | Protocol | 枚举 | 是 | Classic | 可选值：Classic, FD |
        | DataBitRate | int | FD 时 | 2000000 | 数据段波特率 |
        | EnableTermination | bool | 否 | false | 使能设备内置 120 Ω 终端电阻；需要硬件及厂商驱动支持 |
        | ArbitrationSamplePoint | double | 否 | 80.0 | 仲裁段目标采样点百分比，范围 7.5%~97.5%；插件在适配器内部转换为驱动位时序 |
        | DataSamplePoint | double | FD 时 | 80.0 | 数据段目标采样点百分比，范围 7.5%~97.5%；与仲裁段采样点独立配置 |
        | RxQueueSize | int | 否 | 8192 | 接收缓冲区大小（帧数）；NI-XNET 同时由后台接收泵持续排空驱动队列 |
        | ConnectionName | string([ExpressionField]) | 是 | — | 连接标识名，序列内唯一 |

        ## 通道命名规则

        Channel 的格式取决于 AdapterType：

        - NI（NI-XNET）：接口名格式为 `CAN<编号>`，如 `CAN1`、`CAN2`，编号从 1 开始，在 NI MAX 的"设备和接口"中查看（多端口板卡每个端口一个接口名）
        - PEAK（PCAN-Basic）：通道名格式为 `PCAN_USBBUS<编号>`，如 `PCAN_USBBUS1`（也可简写为数字 `1`~`16`），在 PCAN-View 中查看
        - Vector（XL Driver Library）：全局通道索引，如 `0`、`1`，在 Vector Hardware Config 中查看
        - ZLG（周立功）：格式为 `<设备类型>/<设备索引>/<通道索引>`，如 `USBCAN2/0/0`、`USBCANFD-200U/0/1`；设备类型支持 USBCAN1, USBCAN2, USBCAN-E-U, USBCAN-2E-U, USBCANFD-200U, USBCANFD-100U, USBCANFD-MINI
        - Kvaser（CANlib）：通道索引，如 `0`、`1`，在 Kvaser Hardware 工具中查看
        - TOSUN（同星）：通道索引，如 `0`、`1`，默认连接第一个 USB 设备

        注意：运行机器需安装对应厂商驱动（NI-XNET / PCAN-Basic / Vector XL Driver / ZLGCAN / Kvaser Drivers / TSMaster）。

        ## 行为

        - 硬件不存在、通道被占用或同名连接已存在时步骤报错
        - NI-XNET 的 FD+BRS 会话可混合发送经典 CAN 与 CAN FD：经典帧使用 CAN20_Data，FD 帧使用 CANFDBRS_Data；Classic 会话保持 CAN_Data
        - 界面只配置采样点百分比，不暴露厂商专用的 BRP/SJW/TSEG1/TSEG2；适配器内部根据设备时钟与驱动能力换算
        - 运行日志会分别输出 Nominal/Data 两段的目标采样点以及驱动实际采用的位时序；无法表达目标采样点时明确报错，不会静默忽略
        - 经典 CAN 仅使用仲裁段采样点；CAN FD 的仲裁段（Nominal Bit Rate）和数据段（Data Bit Rate）分别使用 ArbitrationSamplePoint 与 DataSamplePoint
        - NI、PEAK、Vector、Kvaser 支持分别按百分比换算仲裁段与 CAN FD 数据段；当前 libTSCAN 及 ZLG CAN FD 标准波特率接口使用 80% 仲裁段和 80% 数据段采样点
        - 软件终端电阻当前接入 NI-XNET、ZLGCAN 与 libTSCAN；PEAK、Vector、Kvaser 请使用外置 120 Ω 电阻
        - NI-XNET 接收会话启动后由后台接收泵持续抽取总线帧，UDS/普通读取按目标 ID 从内存队列路由取帧，未匹配 ID 的帧会保留给后续读取；后台泵异常或队列达到上限丢帧会立即记录诊断

        ## 检索关键词

        CAN、CAN bus、CAN 总线、CAN 2.0、CAN FD、CANFD、DBC、
        NI-XNET、XNET、PEAK、PCAN、PCAN-Basic、PCAN-USB、
        Vector、XL Driver Library、vxlapi、VN1610、
        ZLG、周立功、ZLGCAN、USBCAN、USBCANFD、CANalyst、
        Kvaser、CANlib、TOSUN、同星、TSMaster

        ## 相关插件

        - `CAN_Write` / `CAN_Read`：在此连接上收发报文
        - `CAN_Cyclic_SendStart` / `CAN_Cyclic_SendStop`：周期发送
        - `CAN_Close`：关闭本插件打开的通道
        """;

    public override IStepExecutor CreateExecutor() => new CanOpenExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        var proto = s.Protocol == CanProtocolType.FD ? "FD" : "Classic";
        var termination = s.EnableTermination ? ", 120Ω" : "";
        string samplePoint = s.Protocol == CanProtocolType.FD
            ? $"NominalSP={s.ArbitrationSamplePoint:F1}%, DataSP={s.DataSamplePoint:F1}%"
            : $"SP={s.ArbitrationSamplePoint:F1}%";
        return $"Open {s.ConnectionName} ({s.AdapterType}, {proto}, {s.BaudRate} bps, {samplePoint}{termination})";
    }

	public Task<IReadOnlyList<StepSettingError>> ValidateSettingAsync(
		StepSettingValidationContext context,
		CancellationToken cancellationToken = default)
	{
        var errors = new List<StepSettingError>();
        var s = (CanOpenSetting)CreateSerializer().Deserialize(context.Setting, context.CurrentStep.StepSetting.SettingVersion);
        if (string.IsNullOrWhiteSpace(s.Channel))
            errors.Add(StepSettingError.Error("CAN_001", "通道名称不能为空"));
        if (string.IsNullOrWhiteSpace(s.ConnectionName))
            errors.Add(StepSettingError.Error("CAN_002", "连接标识名不能为空"));
        else if (!context.Evaluator.ValidateExpression(s.ConnectionName, context.ExecutionContext, out var connErr))
            errors.Add(StepSettingError.Error("CAN_004", $"ConnectionName 表达式无效: {connErr}"));
        if (s.BaudRate <= 0)
            errors.Add(StepSettingError.Error("CAN_003", "波特率必须大于 0"));
        if (s.Protocol != CanProtocolType.Classic && s.DataBitRate <= 0)
            errors.Add(StepSettingError.Error("CAN_005", "CAN FD 模式下数据段波特率必须大于 0"));
        else if (s.Protocol != CanProtocolType.Classic && s.DataBitRate < s.BaudRate)
            errors.Add(StepSettingError.Warning("CAN_W01", "数据段波特率通常应大于等于仲裁段波特率"));
        if (s.Protocol == CanProtocolType.FD &&
            (double.IsNaN(s.DataSamplePoint) || double.IsInfinity(s.DataSamplePoint) ||
             s.DataSamplePoint < 7.5 || s.DataSamplePoint > 97.5))
        {
            errors.Add(StepSettingError.Error("CAN_011", "数据段采样点必须在 7.5%~97.5% 之间"));
        }
        if (s.RxQueueSize <= 0)
            errors.Add(StepSettingError.Error("CAN_006", "接收缓冲区大小必须大于 0"));
        else if (s.RxQueueSize < 8192)
            errors.Add(StepSettingError.Warning("CAN_W02", "接收缓冲区小于 NI-XNET 建议值 8192 帧，高负载总线下可能丢帧"));

        if (double.IsNaN(s.ArbitrationSamplePoint) || double.IsInfinity(s.ArbitrationSamplePoint) ||
            s.ArbitrationSamplePoint < 7.5 || s.ArbitrationSamplePoint > 97.5)
        {
            errors.Add(StepSettingError.Error("CAN_007", "仲裁段采样点必须在 7.5%~97.5% 之间"));
        }
        else if (s.AdapterType == CanAdapterType.NI)
        {
            try
            {
                _ = CanBitTimingCalculator.Calculate(s.BaudRate, s.ArbitrationSamplePoint);
            }
            catch (Exception ex)
            {
                errors.Add(StepSettingError.Error("CAN_007", $"NI-XNET 仲裁段采样点无效: {ex.Message}"));
            }
            if (s.Protocol == CanProtocolType.FD)
            {
                try
                {
                    _ = CanBitTimingCalculator.CalculateData(s.DataBitRate, s.DataSamplePoint);
                }
                catch (Exception ex)
                {
                    errors.Add(StepSettingError.Error("CAN_011", $"NI-XNET 数据段采样点无效: {ex.Message}"));
                }
            }
        }
        else if (s.AdapterType == CanAdapterType.TOSUN &&
                 Math.Abs(s.ArbitrationSamplePoint - 80d) > 0.01)
        {
            errors.Add(StepSettingError.Error(
                "CAN_008", "当前 libTSCAN 波特率接口仅能可靠使用 80% 仲裁段采样点"));
        }
        else if (s.AdapterType == CanAdapterType.ZLG && s.Protocol == CanProtocolType.FD &&
                 Math.Abs(s.ArbitrationSamplePoint - 80d) > 0.01)
        {
            errors.Add(StepSettingError.Error(
                "CAN_009", "当前内置 ZLGCAN 的 CAN FD 标准波特率接口仅支持 80% 仲裁段采样点"));
        }
        if (s.Protocol == CanProtocolType.FD &&
            (s.AdapterType is CanAdapterType.TOSUN or CanAdapterType.ZLG) &&
            Math.Abs(s.DataSamplePoint - 80d) > 0.01)
        {
            errors.Add(StepSettingError.Error(
                "CAN_012", $"当前 {s.AdapterType} 的通用 CAN FD 波特率接口仅能可靠使用 80% 数据段采样点"));
        }

        if (s.EnableTermination && s.AdapterType is
            CanAdapterType.PEAK or CanAdapterType.Vector or CanAdapterType.Kvaser)
        {
            errors.Add(StepSettingError.Error(
                "CAN_010", $"{s.AdapterType} 当前通用驱动接口不能控制内置终端电阻，请取消勾选并外接 120 Ω 电阻"));
        }
        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}
