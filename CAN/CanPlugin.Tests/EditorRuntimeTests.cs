using Xunit;

namespace CAN.Tests;

// Real WPF/Syncfusion initialization uses process-wide SDK caches. Do not
// compete with unrelated short-deadline execution tests; their intentional
// concurrency is exercised explicitly inside their own fixtures.
[Collection("CAN blocked I/O")]
public sealed class EditorRuntimeTests
{
    // Syncfusion and WPF pack resources cache Dispatcher-owned objects process-wide.
    // Real views must share one STA, as they do in the platform; separate StaFacts
    // use different threads and can corrupt those caches before the assertions run.
    [StaFact]
    public void Actual_views_save_and_validate_before_refresh_on_the_same_dispatcher()
    {
        if (Environment.GetEnvironmentVariable("CAN_EDITOR_WORKFLOW") == "DBCStagedConstruction")
        {
            DbcStagedRenderTests.VerifyActualPage();
            return;
        }
        if (Environment.GetEnvironmentVariable("CAN_EDITOR_WORKFLOW") == "DBCStagedShutdown")
        {
            DbcStagedRenderTests.VerifyDispatcherShutdown();
            return;
        }
        if (Environment.GetEnvironmentVariable("CAN_EDITOR_WORKFLOW") is "DBCLargeRender" or "DBCLargeReopen")
        {
            DbcLargeRenderTests.VerifyActualPage(Environment.GetEnvironmentVariable("CAN_EDITOR_WORKFLOW") == "DBCLargeReopen");
            return;
        }
        if (Environment.GetEnvironmentVariable("CAN_EDITOR_WORKFLOW") == "DBCRender")
        {
            DbcRenderTests.VerifyActualPages();
            return;
        }
        if (Environment.GetEnvironmentVariable("CAN_EDITOR_WORKFLOW") == "DBCFilePath")
        {
            DbcFilePathEditorTests.VerifyActualPage();
            return;
        }
        if (Environment.GetEnvironmentVariable("CAN_EDITOR_WORKFLOW") == "DBCInitialization")
        {
            DbcEditorInitializationTests.VerifyActualPages();
            return;
        }
        if (Environment.GetEnvironmentVariable("CAN_EDITOR_WORKFLOW") == "DescriptionRefresh")
        {
            EditorDescriptionRefreshTests.VerifyActualPage();
            return;
        }
        if (Environment.GetEnvironmentVariable("CAN_EDITOR_WORKFLOW") == "CanopenCollections")
        {
            CyclicCollectionNotificationTests.VerifyActualPages();
            var collectionEditors = new EditorTests();
            collectionEditors.AllSixEditorsLoadAndLayoutWithIndependentStepTypes();
            collectionEditors.RouteGridEditsPersistAndRemovedRowsCannotChangeOtherSteps();
            collectionEditors.TargetBytesAndOrderSurviveSaveReload();
            collectionEditors.NewMonitoringTrajectoryAndStopFieldsPersistInActualControls();
            return;
        }
        if (Environment.GetEnvironmentVariable("CAN_EDITOR_WORKFLOW") == "RawCyclicSelector")
        {
            RawCyclicProtectionSelectorTests.VerifyActualPage(); return;
        }
        if (Environment.GetEnvironmentVariable("CAN_EDITOR_WORKFLOW") == "TaskStatusJson")
        {
            CanTaskStatusListTests.VerifyActualPage(); return;
        }
        if (Environment.GetEnvironmentVariable("CAN_EDITOR_WORKFLOW") == "ZlgAllHardware")
        {
            ZlgAllHardwareTests.VerifyActualPage(); return;
        }
        if (Environment.GetEnvironmentVariable("CAN_EDITOR_WORKFLOW") == "DBCAllSignals")
        {
            DbcAllSignalsTests.VerifyActualPages();
            return;
        }
        if (Environment.GetEnvironmentVariable("CAN_EDITOR_WORKFLOW") == "DBCExplicitSelection")
        {
            DbcExplicitSelectionTests.VerifyActualPages();
            return;
        }
        if (Environment.GetEnvironmentVariable("CAN_EDITOR_WORKFLOW") == "DBCSelectionSync")
        {
            DbcSelectionSyncTests.VerifyActualPages();
            return;
        }
        if (Environment.GetEnvironmentVariable("CAN_EDITOR_WORKFLOW") == "BatchStatus")
        {
            DbcBatchSelectionTests.VerifyActualPages();
            DbcBatchSelectionTests.WriteProgress("TaskStatus view enter");
            CanTaskStatusListTests.VerifyActualPage();
            DbcBatchSelectionTests.WriteProgress("TaskStatus view exit");
            return;
        }
        if (Environment.GetEnvironmentVariable("CAN_EDITOR_WORKFLOW") == "BackgroundLayout")
        {
            BackgroundLayoutTests.VerifyActualPages();
            return;
        }
        if (Environment.GetEnvironmentVariable("CAN_EDITOR_WORKFLOW") == "DBCLayout")
        {
            DbcLayoutTests.VerifyActualPages();
            return;
        }
        if (Environment.GetEnvironmentVariable("CAN_EDITOR_WORKFLOW") == "DBC")
        {
            DbcEditorTests.VerifyAllEditorWorkflows();
            return;
        }
        if (Environment.GetEnvironmentVariable("CAN_EDITOR_WORKFLOW") == "Appearance")
        {
            CanEditorAppearanceTests.VerifyActualPages();
            return;
        }
        // Permit a separate process to verify this synchronous workflow when
        // an unrelated SDK modal blocks the asynchronous DBC view fixture.
        // The default run still executes every real-view assertion below.
        if (Environment.GetEnvironmentVariable("CAN_EDITOR_WORKFLOW") == "BasicWrite")
        {
            Console.WriteLine("CAN_EDITOR_WORKFLOW=BasicWrite: single/list/cyclic RC/CRC controls and save lifecycle");
            new TimeoutConfigurationTests().Write_view_persists_timeout_and_legacy_cyclic_view_loads_expression_cells();
            CanPayloadProtectionEditorTests.VerifyActualProtectionPages();
            CanE2eDataIdEditorTests.VerifyActualPages();
            CanOfflineCrcTests.VerifyActualPage();
            CanAssistantSettingsTests.VerifyActualPages();
            CanMultiWriteEditorTests.VerifyActualMultiWritePages();
            return;
        }
        Round2UnloadEditorTests.VerifyActualViews();
        FollowupEditorInputTests.VerifySdoPathBeforeRefreshAndUnload();
        EdsEditorLifecycleReauditTests.VerifyActualSdoPageReentry();
        new FlashUiLifecycleReviewTests().The_actual_Flash_page_displays_a_preset_failure_with_wrapping();
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
        FollowupFlashAndDtcUiTests.VerifyActualPages();
        CanPayloadProtectionEditorTests.VerifyActualProtectionPages();
        CanE2eDataIdEditorTests.VerifyActualPages();
        CanOfflineCrcTests.VerifyActualPage();
        CanAssistantSettingsTests.VerifyActualPages();
        CanMultiWriteEditorTests.VerifyActualMultiWritePages();
        CanEditorAppearanceTests.VerifyActualPages();
        DbcEditorTests.VerifyAllEditorWorkflows();
        DbcBatchSelectionTests.VerifyActualPages();
        DbcSelectionSyncTests.VerifyActualPages();
        DbcAllSignalsTests.VerifyActualPages();
        CanTaskStatusListTests.VerifyActualPage();
    }
}
