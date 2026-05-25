using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using MessagePack;
using MessagePack.Resolvers;
using StepEditor.Abstractions;
using WaveformAnalysisPlugin.Models;
using WaveformAnalysisPlugin.UI.Views;
using xTestPlatform.Core.Engine;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using xTestPlatform.Core.Services.ExpressionEngine;

namespace WaveformAnalysisPlugin.UI
{
    public sealed class WaveformAnalysisEditorPlugin : IStepEditorPlugin
    {
        private static readonly MessagePackSerializerOptions _opts =
            MessagePackSerializerOptions.Standard
                .WithCompression(MessagePackCompression.Lz4BlockArray)
                .WithResolver(ContractlessStandardResolver.Instance);

        public string StepTypeId => "WaveformAnalysis";

        public string IconPath =>
            "pack://application:,,,/WaveformAnalysis.StepPlugin.UI;component/Resources/Icons/waveform.png";

        public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
            => new WaveformAnalysisEditorView(step, sequenceFile);

        public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
            byte[] setting,
            IExpressionEvaluator evaluator,
            IExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            var errors = new List<StepSettingError>();

            WaveformAnalysisSetting s;
            try
            {
                s = setting is { Length: > 0 }
                    ? MessagePackSerializer.Deserialize<WaveformAnalysisSetting>(setting, _opts)
                    : new WaveformAnalysisSetting();
            }
            catch { return errors; }

            if (string.IsNullOrWhiteSpace(s.InputVariableName))
                errors.Add(StepSettingError.Error("WFA_INPUT_VAR_EMPTY", "未配置输入变量名"));

            if (s.SampleRate <= 0)
                errors.Add(StepSettingError.Error("WFA_SAMPLE_RATE_INVALID", "采样率必须大于 0"));

            if (s.AnalysisType is AnalysisType.BandPassFilter && s.CutoffFrequencyLow >= s.CutoffFrequencyHigh)
                errors.Add(StepSettingError.Error("WFA_CUTOFF_INVALID", "带通滤波低截止频率必须小于高截止频率"));

            return errors;
        }
    }
}
