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
        public override string Category => "Adapte";
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