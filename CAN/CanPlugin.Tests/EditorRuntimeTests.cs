using Xunit;

namespace CAN.Tests;

public sealed class EditorRuntimeTests
{
    // Syncfusion and WPF pack resources cache Dispatcher-owned objects process-wide.
    // Real views must share one STA, as they do in the platform; separate StaFacts
    // use different threads and can corrupt those caches before the assertions run.
    [StaFact]
    public void Actual_views_save_and_validate_before_refresh_on_the_same_dispatcher()
    {
        Round2UnloadEditorTests.VerifyActualViews();
        new AuditLifecycleTests().ActualOpenViewSavesControlBindingBeforeRefresh();
        new TimeoutConfigurationTests().Open_and_close_views_persist_configured_total_timeout();
        new TimeoutConfigurationTests().Write_view_persists_timeout_and_legacy_cyclic_view_loads_expression_cells();
        new UdsResponseLimitEditorTests().EveryUdsViewExposesAndPersistsResponseLimit();
        new SendBudgetRemediationTests().ActualViewsPersistConfiguredBudgets();
        var cyclic = new EditorTests();
        cyclic.AllSixEditorsLoadAndLayoutWithIndependentStepTypes();
        cyclic.RouteGridEditsPersistAndRemovedRowsCannotChangeOtherSteps();
        cyclic.TargetBytesAndOrderSurviveSaveReload();
        cyclic.NewMonitoringTrajectoryAndStopFieldsPersistInActualControls();
    }
}
