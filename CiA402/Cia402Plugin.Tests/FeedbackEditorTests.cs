using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using CiA402.Models;
using CiA402.UI.Views;
using Xunit;

namespace CiA402.Tests;

public sealed class FeedbackEditorTests
{
    [StaFact]
    public void 绑定页显示已使能只读关联边界且提示可正常换行布局()
    {
        var view = new BindAxisEditorView();
        var plugin = new BindAxisPlugin();
        var (_, state) = TestContext.Create(plugin, new BindAxisSetting());
        view.RefreshFromStep(state.Current!.Step);
        view.Measure(new Size(700, 500));
        view.Arrange(new Rect(0, 0, 700, 500));
        view.UpdateLayout();
        var hint = Assert.IsType<TextBlock>(view.FindName("BindingSafetyHint"));
        Assert.Contains("已使能", hint.Text);
        Assert.Contains("不自动停轴", hint.Text);
        Assert.Equal(TextWrapping.Wrap, hint.TextWrapping);
        Assert.True(hint.ActualHeight > 0);
        Assert.InRange(hint.ActualWidth, 100, 700);
        var timeout = Assert.IsType<TextBox>(view.FindName("ShutdownTimeoutEditor"));
        Assert.Equal("2000", timeout.Text);
        timeout.Text = "7000";
        BindingOperations.GetBindingExpression(timeout, TextBox.TextProperty)!.UpdateSource();
        view.ViewModel.Flush();
        view.RefreshFromStep(state.Current.Step);
        var saved = (BindAxisSetting)plugin.CreateSerializer().Deserialize(state.Current.Step.StepSetting.Setting, state.Current.Step.StepSetting.SettingVersion);
        Assert.Equal(7000, saved.ShutdownTimeoutMs);
        timeout.Text = "0";
        BindingOperations.GetBindingExpression(timeout, TextBox.TextProperty)!.UpdateSource();
        Assert.Contains("清理总超时", view.ViewModel.Error);
    }

    [StaFact]
    public void 多TPDO增删和单元格修改都会保存且页面可布局()
    {
        var plugin = new BindAxisPlugin();
        var (_, state) = TestContext.Create(plugin, new BindAxisSetting());
        var view = new BindAxisEditorView();
        view.RefreshFromStep(state.Current!.Step);
        var model = view.ViewModel;
        model.UseMultipleFeedbackPdos = true;
        model.AddFeedbackPdoCommand.Execute(null);
        model.AddFeedbackPdoCommand.Execute(null);
        Assert.Equal(2, model.FeedbackPdos.Count);
        view.Measure(new Size(1000, 850));
        view.Arrange(new Rect(0, 0, 1000, 850));
        view.UpdateLayout();
        var grid = Assert.IsType<DataGrid>(view.FindName("FeedbackPdoGrid"));
        Assert.Equal(Visibility.Visible, grid.Visibility);
        Assert.InRange(grid.ActualHeight, 100, 260);
        Assert.Equal(2, grid.Items.Count);
        Assert.False(grid.CanUserSortColumns); // 界面顺序必须与重复字段的来源优先级一致。
        var row = model.FeedbackPdos[0];
        grid.SelectedItem = row;
        grid.CurrentCell = new(row, grid.Columns[1]);
        grid.ScrollIntoView(row);
        grid.UpdateLayout();
        Assert.True(grid.BeginEdit());
        var editor = Assert.IsType<TextBox>(grid.Columns[1].GetCellContent(row));
        editor.Text = "0x6041:0:16,0x6061:0:8,0x6064:0:32";
        BindingOperations.GetBindingExpression(editor, TextBox.TextProperty)!.UpdateSource();
        Assert.True(grid.CommitEdit(DataGridEditingUnit.Cell, true));
        model.Flush();
        var serializer = plugin.CreateSerializer();
        var saved = (BindAxisSetting)serializer.Deserialize(state.Current.Step.StepSetting.Setting, state.Current.Step.StepSetting.SettingVersion);
        Assert.Equal(2, state.Current.Step.StepSetting.SettingVersion);
        Assert.Equal(editor.Text, saved.FeedbackPdos[0].Layout);
        Assert.True(saved.UseMultipleFeedbackPdos);
        Assert.Empty(model.Error);
        model.SelectedFeedbackPdo = model.FeedbackPdos[1];
        model.RemoveFeedbackPdoCommand.Execute(null);
        model.Flush();
        Assert.Single(((BindAxisSetting)serializer.Deserialize(state.Current.Step.StepSetting.Setting, 2)).FeedbackPdos);
    }

    [StaFact]
    public void 删除清空刷新后旧行变更不会触发其他步骤保存()
    {
        var plugin = new BindAxisPlugin();
        var (_, first) = TestContext.Create(plugin, new BindAxisSetting());
        var (_, second) = TestContext.Create(plugin, new BindAxisSetting());
        var view = new BindAxisEditorView();
        view.RefreshFromStep(first.Current!.Step);
        var model = view.ViewModel;
        var saves = 0;
        model.ExecuteCommand = (_, action) => { saves++; action(); };
        model.UseMultipleFeedbackPdos = true;
        model.AddFeedbackPdoCommand.Execute(null);
        var removed = model.FeedbackPdos[0];
        model.FeedbackPdos.Clear();
        model.Flush();
        var count = saves;
        removed.Number = 17;
        model.Flush();
        Assert.Equal(count, saves);
        model.AddFeedbackPdoCommand.Execute(null);
        var previous = model.FeedbackPdos[0];
        model.Flush();
        view.RefreshFromStep(second.Current!.Step);
        count = saves;
        previous.Layout = "0x606C:0:32";
        model.Flush();
        Assert.Equal(count, saves);
        Assert.Empty(model.FeedbackPdos);
        Assert.False(model.UseMultipleFeedbackPdos);
    }

    [StaFact]
    public void 从旧单TPDO启用多帧会保留原映射且开关不清空其他配置()
    {
        var plugin = new BindAxisPlugin();
        var (_, state) = TestContext.Create(plugin, new BindAxisSetting { FeedbackPdoNumber = 3 });
        var view = new BindAxisEditorView();
        view.RefreshFromStep(state.Current!.Step);
        var model = view.ViewModel;
        model.UseMultipleFeedbackPdos = true;
        Assert.Equal(3, Assert.Single(model.FeedbackPdos).Number);
        Assert.False(model.IsSingleFeedbackPdo);
        Assert.True(model.IsMultipleFeedbackPdos);
        model.Transport = TransportKind.EtherCAT;
        Assert.False(model.IsMultipleFeedbackPdos);
        model.Transport = TransportKind.CANopen;
        model.UseMultipleFeedbackPdos = false;
        model.Flush();
        Assert.True(model.IsSingleFeedbackPdo);
        Assert.Equal(3, model.FeedbackPdoNumber);
        Assert.Single(model.FeedbackPdos);
    }
}
