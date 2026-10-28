using System.Windows;
using System.Windows.Controls;
using OpcUa.Models;
using xTestPlatform.Core.Engine;
using StepEditor.Abstractions;
using xTestPlatform.Core.SequenceModels;
using xTestPlatform.Core.Services.ExpressionEngine;
using Xunit;

namespace OpcUa.Tests;

public sealed class GridContractTests
{
    [Fact]
    public void ActualBatchWriteTypeComboEditsAndPersistsEnum() => UiThread.Run(() =>
    {
        var plugin = new OpcUaBatchWritePlugin(); var serializer = plugin.CreateSerializer(); var step = new Step();
        step.StepSetting.Setting = serializer.Serialize(new OpcUaBatchWriteSetting { Items = [new() { NodeId = "ns=2;s=V" }] }); step.StepSetting.SettingVersion = serializer.SettingVersion;
        var page = (UserControl)new OpcUa.UI.OpcUaBatchWriteEditorPlugin().CreateEditor(step, new SequenceFile());
        page.Measure(new Size(1200, 800)); page.Arrange(new Rect(0, 0, 1200, 800)); page.UpdateLayout();
        var grid = Assert.IsType<DataGrid>(page.FindName("ItemsGrid")); var column = Assert.Single(grid.Columns.OfType<DataGridComboBoxColumn>());
        var item = grid.Items[0]; grid.SelectedItem = item; grid.CurrentCell = new DataGridCellInfo(item, column); grid.ScrollIntoView(item, column); grid.UpdateLayout();
        Assert.True(grid.BeginEdit()); grid.UpdateLayout();
        var combo = Assert.IsType<ComboBox>(column.GetCellContent(item)); Assert.Contains(OpcUaDataType.String, combo.Items.Cast<object>());
        combo.SelectedItem = OpcUaDataType.String;
        page.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        ((IRefreshableEditor)page).RefreshFromStep(step);
        var saved = (OpcUaBatchWriteSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
        Assert.Equal(OpcUaDataType.String, saved.Items[0].DataType);
    });

    [Fact]
    public async Task ActualStartValidatorPermitsDocumentedNodeIdColumnFallback()
    {
        var plugin = new OpcUaDataAcqStartPlugin(); var setting = new OpcUaDataAcqStartSetting { Items = [new() { NodeId = "ns=2;s=V", ColumnName = "" }] };
        var (context, _) = BoundaryContext.Create(plugin, setting);
        var errors = await new OpcUa.UI.OpcUaDataAcqStartEditorPlugin().ValidateWithContextAsync(new StepEditorValidationContext
        { Setting = plugin.CreateSerializer().Serialize(setting), Evaluator = ExpressionEvaluatorFactory.Default, ExecutionContext = context, SequenceFile = new SequenceFile(), Block = [], CurrentStep = new Step() });
        Assert.DoesNotContain(errors, error => error.Message.Contains("列名不能为空"));
    }
}
