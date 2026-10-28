using System.Windows;
using CAN.CANopen.Cyclic;
using CAN.UI.Validation;
using CAN.UI.Views;
using CAN.Runtime;
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
        var s = (CyclicSetting)Plugin.CreateSerializer().Deserialize(context.Setting,
            context.CurrentStep?.StepSetting.SettingVersion ?? Plugin.CreateSerializer().SettingVersion);
        void Expr(string text, string field) => CanopenValidation.Expr(context, text, field, "CY_EXPR", errors);
        void Positive(int value, string field) => CanopenValidation.Positive(value, field, "CY_RANGE", errors);
        Expr(s.ConnectionName, "ConnectionName"); Expr(s.ServiceName, "ServiceName");
        if (Plugin.Operation == CyclicOperation.ServiceStart)
        {
            if ((!s.EnableMonitoringSync && s.FeedbackRoutes.Count == 0) || s.FeedbackRoutes.Count > 128)
                errors.Add(StepSettingError.Error("CY_ROUTES", "反馈路由需要1~128行；独立周期SYNC可不配置读取"));
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
            if (s.RequireOnBusConfirmation) errors.Add(StepSettingError.Warning("CY_DIAGNOSTIC_CONFIRM", "逐批发送确认仅用于独占诊断，会等待回显/会话完成并阻塞周期；正常PDO/SYNC使用仅提交"));
            if (s.FeedbackStartupGraceMs < 0) errors.Add(StepSettingError.Error("CY_GRACE", "反馈启动宽限不能为负"));
            Expr(s.StopCommandsJson, "停机 SDO JSON"); Positive(s.StopTimeoutMs, "停机总期限");
            if (s.UseFiniteTrajectory)
            {
                Expr(s.TrajectoryJson, "点表 JSON"); Positive(s.TrajectoryDurationMs, "点表总持续时间");
                if (!Enum.IsDefined(s.MissPolicy) || s.LatenessWindowMs < 0 || s.MaxSkippedPoints < 0 || s.HoldTimeoutMs <= 0)
                    errors.Add(StepSettingError.Error("CY_TRAJECTORY_POLICY", "点表策略必须有效，窗口/跳点上限不能为负，保持期限须为正"));
                if (s.MissPolicy != TrajectoryMissPolicy.Fail)
                    errors.Add(StepSettingError.Warning("CY_TRAJECTORY_RAW", "跳点/保持只处理原始字节，不能校验速度/加速度连续性；需要序列规划层确认精度损失。HoldLast还须配置停机命令"));
            }
        }
        if (Plugin.Operation == CyclicOperation.ServiceStart && s.EnableMonitoringSync)
        {
            if (s.MonitoringConsecutiveLatenessLimit is < 1 or > 10000) errors.Add(StepSettingError.Error("CY_MONITOR_LATE_LIMIT", "仅监测连续迟到故障次数必须为1~10000"));
            Expr(s.SyncCobId, "SYNC ID"); Expr(s.SyncData, "SYNC 数据");
            Positive(s.MonitoringPeriodMs, "监测 SYNC 周期"); Positive(s.CycleTimeoutMs, "发送提交/确认总期限"); Positive(s.StopTimeoutMs, "启动失败退出期限");
            if (s.RequireOnBusConfirmation) errors.Add(StepSettingError.Warning("CY_DIAGNOSTIC_CONFIRM", "逐批确认诊断不能与其它周期发送源并行，正常SYNC使用仅提交"));
            if (s.MaxLatenessMs < 0) errors.Add(StepSettingError.Error("CY_LATENESS", "迟到容忍不能为负"));
        }
        if (Plugin.Operation is CyclicOperation.CycleStop or CyclicOperation.ServiceStop) Positive(s.StopTimeoutMs, "退出期限");
        if (Plugin.Operation == CyclicOperation.SnapshotRead)
            CanopenValidation.Variable(context, s.ResultVariable, v => v is string, "string JSON", "CY_RESULT", errors);
        if (context.CurrentStep is { } currentStep)
            CanLifecycleValidator.CheckPrecedingOpen(context.SequenceFile, context.Block, currentStep, s.ConnectionName, errors);
        return Task.FromResult<IReadOnlyList<StepSettingError>>(errors);
    }
}

