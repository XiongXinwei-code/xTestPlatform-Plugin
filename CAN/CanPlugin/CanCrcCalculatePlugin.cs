using CAN.Executors;
using CAN.Helpers;
using CAN.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace CAN;

public sealed class CanCrcCalculatePlugin : CAN.PluginInfrastructure.StrictStepPluginBase<CanCrcCalculateSetting>
{
    public override string StepTypeId => "IO.CanCrcCalculate";
    public override string DisplayName => "CAN_Calculation_CRC Calculate";
    public override string Category => "Communication";
    public override string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can_write.png";
    public override string Description => """
        ## 功能
        离线计算 CAN/FD 载荷的应用层 CRC。无需 CAN_Open，不发送或读取总线报文，不改动输入数据。

        ## 参数
        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |---|---|---|---|---|
        | Data | string([ExpressionField]) | 是 | "00 00 00 00 00 00 00 00" | 求值结果为 string，表示完整帧的 Hex 载荷，最多 64 字节；CRC 字段偏移仍相对于完整载荷 |
        | Protection | CanPayloadProtectionSetting | 是 | CRC-8/AUTOSAR，CRC 字节 0，计算字节 1 至末尾 | 复用固定 Data ID、P1/P2、自定义算法与范围配置；必须启用 CRC；读取现有 Counter 和显式 NIBBLE，不应用 RC 生成初值/增量 |
        | ResultVariable | string(VariablePathField) | 否 | 空 | 可选，填写已声明字符串变量完整路径；存入 0x 开头、按 CRC 位宽补零的大写 Hex 数值 |
        | TimeoutMs | int | 是 | 5000 | 本步骤包括表达式求值在内的总期限，必须大于 0 ms |

        ## 行为
        - P2 表可来自内联 16 项表或表达式；表达式求值结果必须为恰好 16 项的 byte[]，每次步骤运行求值并复制冻结。
        - 仅计算期望 CRC 数值；不比较输入 CRC、不改写 RC/CRC/NIBBLE，也不进行完整 E2E 状态监督。CRC 结果字节序设置不改变返回的数值。
        - 成功返回 Passed 和 Hex 结果，错误/内部超时返回中文 Error，调用方取消返回 Aborted。旧求值迟到时不发布结果。
        - DLL 公开 CAN.Helpers.CanCrcFunctions.Calculate(byte[], setting) 与 E2E_Crc8(byte[], byte)。后者的数据必须已经选好计算范围，末尾追加指定 Data ID，固定 2F/FF/FF、无反射。
        - SDK 没有插件自定义全局函数注册接口；这些函数不会自动成为表达式中的裸 E2E_Crc8 名称。平台需要显式引用本 DLL/类型才能直接调用；本步骤不依赖该能力。
        """ + CanPayloadProtectionDescription.Parameters;
    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("CAN", () => new CanCrcCalculateExecutor());
    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"Offline CRC {s.Data}" + (string.IsNullOrWhiteSpace(s.ResultVariable) ? "" : $" → {s.ResultVariable}");
    }
}
