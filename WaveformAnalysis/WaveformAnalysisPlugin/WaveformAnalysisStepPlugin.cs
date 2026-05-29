using System.IO;
using WaveformAnalysisPlugin.Execution;
using WaveformAnalysisPlugin.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;

namespace WaveformAnalysisPlugin
{
    /// <summary>
    /// 波形分析步骤插件（核心层）。
    /// 不含任何 WPF / UI 依赖。
    /// </summary>
    public sealed class WaveformAnalysisStepPlugin : StepPluginBase<WaveformAnalysisSetting>
    {
        protected override int CurrentSettingVersion => 1;

        public override string StepTypeId => "WaveformAnalysis";
        public override string DisplayName => "Waveform Analysis";
        public override string Description => "波形分析步骤，支持多种分析类型（如峰值检测、频谱分析等）。输入数据应为数值数组，输出结果根据分析类型不同而异。适用于需要对测试数据进行深入分析的场景。";
        public override string Category => "Analysis";
        public override string IconPath =>
            "pack://application:,,,/WaveformAnalysis.StepPlugin.UI;component/Resources/Icons/waveform.png";

        public override IStepExecutor CreateExecutor() => new WaveformAnalysisExecutor();

        public override string GenerateDescription(byte[] setting)
        {
            var s = DeserializeSetting(setting);
            return $"波形分析: {s.AnalysisType}";
        }
    }
}
