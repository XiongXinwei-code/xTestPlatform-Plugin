using System.Windows;
using CAN.Helpers;
using CAN.Models;
using CAN.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI;

public sealed class CanOpenEditorPlugin : IStepEditorPlugin
{
    public string StepTypeId => "IO.CanOpen";
    public string IconPath => "pack://application:,,,/CAN.StepPlugin.UI;component/Resources/Icons/can.png";

    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new CanOpenEditorView();
        view.SequenceFile = sequenceFile;
        view.ViewModel.AttachSerializer(new CanOpenPlugin().CreateSerializer());
        view.ViewModel.AttachStep(step);
        return view;
    }

    public async Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(
		StepEditorValidationContext context, CancellationToken ct = default)
    {
        var errors = new List<StepSettingError>();
        var s = (CanOpenSetting)new CanOpenPlugin().CreateSerializer().Deserialize(context.Setting, 1);
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
        return errors;
    }
}
