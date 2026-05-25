using System.IO;
using WaveformAnalysisPlugin.Execution;
using WaveformAnalysisPlugin.Models;
using xTestPlatform.Core.Plugins.BuiltIn;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

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
        public override string DisplayName => "波形分析";
        public override string Category => "Analysis";
        public override string IconPath =>
            "pack://application:,,,/WaveformAnalysis.StepPlugin.UI;component/Resources/Icons/waveform.png";

        public override IStepExecutor CreateExecutor() => new WaveformAnalysisExecutor();

        public override string GenerateDescription(byte[] setting)
        {
            var s = DeserializeSetting(setting);
            return $"波形分析: {s.AnalysisType}";
        }

        public override IReadOnlyList<Variables> GetDefaultStepVariables()
            => [StepVariableProfiles.CreateResultCluster()];
    }
}
