using System.Windows;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using ZlgEtherCat.Models;
using ZlgEtherCat.Plugins;

namespace ZlgEtherCat.UI.Editors;

public abstract class ZlgEditorPluginBase<T> : IStepEditorPlugin where T : ZlgSetting
{
    public abstract string StepTypeId { get; }
    public abstract string IconPath { get; }
    public abstract FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile);
    public abstract Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(StepEditorValidationContext context, CancellationToken ct = default);

    protected static void ValidateExpression(string expression, string label, StepEditorValidationContext context, List<StepSettingError> errors)
    {
        if (!string.IsNullOrWhiteSpace(expression) && !context.Evaluator.ValidateExpression(expression, context.ExecutionContext, out var error))
            errors.Add(StepSettingError.Error("ZEC_EXPRESSION", $"{label}表达式无效：{error}"));
    }

    protected static void ValidateVariable(string variable, bool required, string label, StepEditorValidationContext context, List<StepSettingError> errors)
    {
        if (string.IsNullOrWhiteSpace(variable))
        {
            if (required) errors.Add(StepSettingError.Error("ZEC_VARIABLE", $"{label}不能为空"));
            return;
        }
        if (!context.ExecutionContext.HasVariable(variable))
            errors.Add(StepSettingError.Error("ZEC_VARIABLE", $"{label}未在当前变量作用域声明：{variable}"));
    }

    protected static void ValidatePreceding(StepEditorValidationContext context, string prerequisite, string connection, List<StepSettingError> errors, string resource = "", string prerequisiteLabel = "")
    {
        if (string.IsNullOrWhiteSpace(prerequisiteLabel)) prerequisiteLabel = prerequisite.Replace("ZlgEtherCat.", "", StringComparison.Ordinal);
        string? ending = prerequisite switch
        {
            "ZlgEtherCat.Open" => "ZlgEtherCat.Close",
            "ZlgEtherCat.StartAcquisition" => "ZlgEtherCat.StopAcquisition",
            "ZlgEtherCat.BackgroundPoolCreate" => "ZlgEtherCat.BackgroundPoolClose",
            "ZlgEtherCat.PeriodicPoolCreate" => "ZlgEtherCat.PeriodicPoolClose",
            "ZlgEtherCat.BindAxis" => "ZlgEtherCat.UnbindAxis",
            "ZlgEtherCat.StartBus" => "ZlgEtherCat.StopBus",
            "ZlgEtherCat.EnablePi" => "ZlgEtherCat.DisablePi",
            "ZlgEtherCat.StartCyclicOutput" => "ZlgEtherCat.StopCyclicOutput",
            "ZlgEtherCat.StartCapture" => "ZlgEtherCat.StopCapture",
            "ZlgEtherCat.StartBoardPwm" => "ZlgEtherCat.StopBoardPwm",
            "ZlgEtherCat.ConfigureBoardPulse" => "ZlgEtherCat.StopBoardPulse",
            "ZlgEtherCat.StartBoardLatch" => "ZlgEtherCat.StopBoardLatch",
            _ => null
        };
        int current = context.Block.IndexOf(context.CurrentStep);
        for (int i = current - 1; i >= 0; i--)
        {
            var step = context.Block[i];
            if (!step.StepSetting.StepType.StartsWith("ZlgEtherCat.", StringComparison.Ordinal)) continue;
            try
            {
                var plugin = ZlgStepCatalog.Create(step.StepSetting.StepType);
                var grouped = (IZlgGroupedSetting)plugin.CreateSerializer().Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
                string actionId = "ZlgEtherCat." + grouped.ActiveOperation;
                if (actionId != prerequisite && actionId != ending) continue;
                var setting = grouped.CreateOperationSetting();
                if (setting.ConnectionExpression != connection || (resource.Length > 0 && setting.ResourceExpression != resource)) continue;
                if (actionId == prerequisite)
                {
                    if (prerequisite == "ZlgEtherCat.RequestState" && setting is global::ZlgEtherCat.Models.Operations.RequestStateSetting state && state.State != MasterState.Operational)
                    {
                        errors.Add(StepSettingError.Warning("ZEC_LC_STATE", "最近的主站状态请求不是 OP，启用过程映像前需要请求并确认 OP"));
                        return;
                    }
                    return;
                }
                errors.Add(StepSettingError.Warning("ZEC_LC_CLOSED", $"同连接/资源最近已执行 {ending?.Replace("ZlgEtherCat.", "", StringComparison.Ordinal)}，请先重新执行 {prerequisiteLabel}"));
                return;
            }
            catch (Exception ex)
            {
                errors.Add(StepSettingError.Warning("ZEC_LC_PARSE", $"前置步骤配置无法解析：{ex.Message}"));
            }
        }
        errors.Add(StepSettingError.Warning("ZEC_LC01", $"当前步骤之前未找到同连接的 {prerequisiteLabel}；动态分支仍在执行时校验实际资源"));
    }
}
