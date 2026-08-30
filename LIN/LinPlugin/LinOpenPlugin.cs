using LIN.Executors;
using LIN.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace LIN;

public sealed class LinOpenPlugin : StepPluginBase<LinOpenSetting>
{
    public override string StepTypeId   => "IO.LinOpen";
    public override string DisplayName  => "LIN_Open";
    public override string Category     => "Communication";
    public override string IconPath     => "pack://application:,,,/LIN.StepPlugin.UI;component/Resources/Icons/lin.png";

    public override string Description => """
        ## 功能

        打开 LIN 通道并建立连接，支持 LIN 1.x 和 LIN 2.x 协议，可配置为主节点或从节点模式。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | AdapterType | 枚举 | 否 | NI | 可选值：NI（已实现）；PEAK / Vector / IXXAT 枚举已定义但适配器未实现，选用会在运行时抛 NotSupportedException |
        | Channel | string([ExpressionField]) | 是 | "LIN1" | 硬件通道名称 |
        | BaudRate | int | 否 | 19200 | 波特率 |
        | LinVersion | 枚举 | 否 | LIN_2x | 可选值：LIN_1x, LIN_2x |
        | IsMaster | bool | 否 | true | 是否主节点 |
        | RxQueueSize | int | 否 | 512 | 接收缓冲区大小（帧数），驱动层接收队列容量，两次读取之间到达的帧缓存在此，队列满后新帧丢弃 |
        | ConnectionName | string([ExpressionField]) | 是 | "LIN1" | 运行时连接标识名，供后续步骤引用 |

        ## 行为

        - 打开后通过 ConnectionName 标识连接，供 Read/Write/Cyclic 步骤使用

        ## 适用硬件

        本插件按**协议**命名，不按厂商命名：所有 LIN 硬件统一通过本插件接入，具体厂商由 `AdapterType` 字段选择。
        平台中不存在、也不需要按厂商或驱动命名的独立 LIN 插件（如 NiLin、XNET 插件）——需要 LIN 通信时一律使用 `LIN_*` 系列步骤。

        - `NI`：经 NI-XNET 驱动（`nixnet.dll`）访问 NI XNET 系列 LIN 接口硬件，例如 USB-8506。需预先安装 NI-XNET 驱动。
        - `PEAK` / `Vector` / `IXXAT`：尚未实现。

        ## 检索关键词

        LIN、LIN bus、LIN 总线、局部互联网络、LIN 主节点、LIN 从节点、LDF、
        NI-XNET、XNET、nixnet.dll、NI USB-8506、USB-8506、NI LIN 接口卡

        ## 相关插件

        - `LIN_Close`：关闭 LIN 通道
        - `LIN_Read` / `LIN_Write`：收发 LIN 帧
        """;

    public override IStepExecutor CreateExecutor() => new LinOpenExecutor();

    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Open {s.ConnectionName} ({s.AdapterType}, LIN {s.LinVersion}, {s.BaudRate} bps, {(s.IsMaster ? "主节点" : "从节点")})";
    }
}
