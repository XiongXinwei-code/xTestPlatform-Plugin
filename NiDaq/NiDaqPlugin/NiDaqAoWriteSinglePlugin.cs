using NiDaq.Executors;
using NiDaq.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace NiDaq;

public sealed class NiDaqAoWriteSinglePlugin : StepPluginBase<NiDaqAoWriteSingleSetting>
{
    public override string StepTypeId => "NiDaq.AoWriteSingle";
    public override string DisplayName => "NiDaq_AO_WriteSingle";
    public override string Category => "DataAcquisition";
    public override string IconPath => "pack://application:,,,/NiDaq.StepPlugin.UI;component/Resources/Icons/nidaq_ao_write_single.png";
    public override string Description => """
        ## 功能

        向已配置且成功 TaskStart 的 AO 电压任务写入一组单点值；不创建新任务、不自动启动、不改变通道顺序。

        ## 参数

        | 参数 | 类型 | 必填 | 默认值 | 说明 |
        |------|------|------|--------|------|
        | TaskName | string([ExpressionField]) | 是 | "AoTask1" | 求值结果为 string，必须对应 AO_Config 的任务 |
        | Values | string([ExpressionField]) | 是 | 空 | 求值结果为数值或数值集合，如 new double[] { 1.0, 2.0 }；单位 V，按通道配置行序 |
        | TimeoutMs | int | 否 | 10000 | 单次总等待期限 ms，包含表达式求值、排队和 SDK 调用；大于 0 |

        ## 行为与结果

        - 写前复制整组数值并校验数量、有限性和每通道量程；不截断越界值，不静默补零。
        - 同一任务串行执行；超时/取消不表示底层写入已撤回，保留真实调用，随后必须 TaskStop 确认收尾。
        - Completed 的 Value 给出请求电压与“SDK 已返回”，不是实测电压，也不是设备安全确认。
        - Stop/中断禁止新目标写入，终值及停止行为使用 AO_Config 中明确选择的策略。

        ## 相关插件

        - `NiDaq_AO_Config` → `NiDaq_Task_Start` → 本步骤 → `NiDaq_Task_Stop`。
        """;
    public override IStepExecutor CreateExecutor() => PluginCommon.ExecutionScopedExecutor.Create("NiDaq", () => new NiDaqAoWriteSingleExecutor());
    public override string GenerateDescription(byte[] setting)
    {
        var s = DeserializeSetting(setting);
        return $"AO WriteSingle: {s.TaskName} ← {s.Values} ({s.TimeoutMs} ms)";
    }
}
