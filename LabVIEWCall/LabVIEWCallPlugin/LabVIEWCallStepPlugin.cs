using LabVIEWCallPlugin.Execution;
using LabVIEWCallPlugin.Models;
using System.IO;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace LabVIEWCallPlugin
{
    /// <summary>
    /// LabVIEW Call 步骤插件（核心层）。
    /// 只负责执行器创建和元数据描述，不含任何 WPF / UI 依赖。
    /// 所有校验逻辑均在 LabVIEWCallEditorPlugin（UI 层）中实现。
    /// </summary>
    public sealed class LabVIEWCallStepPlugin : StepPluginBase<LabVIEWCallSetting>
    {
        protected override int CurrentSettingVersion => 1;

        public override string StepTypeId => "LabVIEWCall";
        public override string DisplayName => "LabVIEW Call";

        public override string Description => """
            ## 功能

            调用 LabVIEW 虚拟仪器（VI）文件，执行其中定义的功能。适用于需要在测试流程中集成 LabVIEW 功能的场景。

            ## 参数

            | 参数 | 类型 | 必填 | 默认值 | 说明 |
            |------|------|------|--------|------|
            | ViFilePath | string | 是 | — | VI 文件完整路径 |
            | ShowPanel | bool | 否 | false | 调用时是否显示前面板 |
            | ClosePanel | bool | 否 | false | 执行完毕后是否关闭前面板 |
            | TimeoutMs | int | 否 | 0 | VI 执行超时时间（毫秒），0 表示不限制 |
            | InputParameters | string | 否 | 空 | VI 输入控件参数的 JSON 序列化字符串，由编辑器自动生成 |
            | OutputParameters | string | 否 | 空 | VI 输出指示器参数的 JSON 序列化字符串，由编辑器自动生成 |

            ## 行为

            - 需要本机安装 LabVIEW 运行时环境
            - VI 文件不存在或加载失败时步骤报错
            - VI 调用为阻塞式非托管调用，无法响应取消；配置 `TimeoutMs` 后超时会终止等待并返回 Error
            - `TimeoutMs` 为 0 时将一直等待 VI 返回，若 VI 可能不返回请务必配置超时
            """;
        public override string Category => "Adapter";
        public override string IconPath =>
                        "pack://application:,,,/LabVIEWCall.StepPlugin.UI;component/Resources/Icons/labview.png";

        public override IStepExecutor CreateExecutor() => new LabVIEWCallExecutor();

        public override string GenerateDescription(byte[] setting)
        {
            var s = DeserializeSetting(setting);
            return string.IsNullOrWhiteSpace(s.ViFilePath)
                ? "LabVIEW Call"
                : $"Call: {Path.GetFileName(s.ViFilePath)}";
        }
    }
}