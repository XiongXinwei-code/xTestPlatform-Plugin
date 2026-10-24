using System.Windows;
using CAN.CANopen.Cyclic;
using CAN.UI.Validation;
using CAN.UI.Views;
using StepEditor.Abstractions;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace CAN.UI;

public abstract class CyclicServiceEditorBase : IStepEditorPlugin
{
    protected abstract CyclicPluginBase Plugin { get; }
    public string StepTypeId => Plugin.StepTypeId;
    public string IconPath => Plugin.IconPath;
    public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
    {
        var view = new CyclicServiceEditorView(Plugin) { SequenceFile = sequenceFile };
        view.RefreshFromStep(step);
        return view;
    }
    public Task<IReadOnlyList<StepSettingError>> ValidateWithContextAsync(StepEditorValidationContext context, CancellationToken cancellationToken = default)
    {
        var errors = new List<StepSettingError>();
        var s = (CyclicSetting)Plugin.CreateSerializer().Deserialize(context.Setting, 1);
        void Expr(string text, string field) => CanopenValidation.Expr(context, text, field, "CY_EXPR", errors);
        void Positive(int value, string field) => CanopenValidation.Positive(value, field, "CY_RANGE", errors);
        Expr(s.ConnectionName, "ConnectionName"); Expr(s.ServiceName, "ServiceName");
        if (Plugin.Operation == CyclicOperation.ServiceStart)
        {
            if (s.FeedbackRoutes.Count == 0 || s.FeedbackRoutes.Count > 128) errors.Add(StepSettingError.Error("CY_ROUTES", "反馈路由需要 1~128 行"));
            if (s.FeedbackRoutes.Select(r => r.Name).Distinct().Count() != s.FeedbackRoutes.Count)
                errors.Add(StepSettingError.Error("CY_NAMES", "反馈路由名称不能重复"));
            foreach (var route in s.FeedbackRoutes)
            {
                Expr(route.CobId, "COB-ID"); Positive(route.MaxAgeMs, "有效期");
                if (string.IsNullOrWhiteSpace(route.Name) || route.Dlc is < 0 or > 8) errors.Add(StepSettingError.Error("CY_ROUTE", "名称不能为空，DLC 必须为 0~8"));
            }
        }
        if ((Plugin.Operation == CyclicOperation.CycleStart && !s.UseFiniteTrajectory) || Plugin.Operation == CyclicOperation.CycleInputsUpdate)
        {
            if (s.Targets.Count is 0 or > 128) errors.Add(StepSettingError.Error("CY_TARGETS", "RPDO 需要 1~128 行"));
            foreach (var target in s.Targets)
            {
                Expr(target.CobId, "RPDO ID"); Expr(target.Data, "数据");
                if (target.Dlc is < 0 or > 8) errors.Add(StepSettingError.Error("CY_DLC", "DLC 必须为 0~8"));
            }
        }
        if (Plugin.Operation == CyclicOperation.CycleStart)
        {
            Expr(s.NodeIds, "NodeIds"); Expr(s.SyncCobId, "SYNC ID"); Expr(s.SyncData, "SYNC 数据");
            Positive(s.PeriodMs, "周期"); Positive(s.CycleTimeoutMs, "周期期限"); Positive(s.InputMaxAgeMs, "输入有效期");
            if (s.MaxLatenessMs < 0) errors.Add(StepSettingError.Error("CY_LATENESS", "迟到容忍不能为负"));
            if (!s.RequireOnBusConfirmation) errors.Add(StepSettingError.Warning("CY_HOST_ONLY", "只验证主机提交顺序；不保证物理顺序，未知待发帧会阻止停止确认"));
            if (s.FeedbackStartupGraceMs < 0) errors.Add(StepSettingError.Error("CY_GRACE", "反馈启动宽限不能为负"));
            Expr(s.StopCommandsJson, "停机 SDO JSON"); Positive(s.StopTimeoutMs, "停机总期限");
            if (s.UseFiniteTrajectory) { Expr(s.TrajectoryJson, "点表 JSON"); Positive(s.TrajectoryDurationMs, "点表总持续时间"); }
        }
        if (Plugin.Operation == CyclicOperation.ServiceStart && s.EnableMonitoringSync)
        {
            Expr(s.SyncCobId, "SYNC ID"); Expr(s.SyncData, "SYNC 数据");
            Positive(s.MonitoringPeriodMs, "监测 SYNC 周期"); Positive(s.CycleTimeoutMs, "发送确认期限"); Positive(s.StopTimeoutMs, "启动失败退出期限");
            if (s.MaxLatenessMs < 0) errors.Add(StepSettingError.Error("CY_LATENESS", "迟到容忍不能为负"));
        }
        if (Plugin.Operation is CyclicOperation.CycleStop or CyclicOperation.ServiceStop) Positive(s.StopTimeoutMs, "退出期限");
        if (Plugin.Operation == CyclicOperation.SnapshotRead)
            CanopenValidation.Variable(context, s.ResultVariable, v => v is string, "string JSON", "CY_RESULT", errors);
        CanLifecycleValidator.CheckPrecedingOpen(context.SequenceFile, context.Block, context.CurrentStep, s.ConnectionName, errors);
        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}

