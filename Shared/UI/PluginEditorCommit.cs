using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace PluginCommon.UI;

/// <summary>保留文本防抖；焦点离开、选择和按钮操作完成后提交当前编辑草稿。</summary>
public static class PluginEditorCommit
{
    private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached(
        "State", typeof(CommitState), typeof(PluginEditorCommit));

    public static void Attach(FrameworkElement editor, Action flush)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(flush);
        editor.Dispatcher.VerifyAccess();
        if (editor.GetValue(StateProperty) is CommitState previous) previous.Detach();
        var state = new CommitState(editor, flush);
        editor.SetValue(StateProperty, state);
        state.Attach();
    }

    private sealed class CommitState(FrameworkElement editor, Action flush)
    {
        private bool _committing;
        private RoutedEventHandler? _commitHandler;
        private SelectionChangedEventHandler? _selectionHandler;
        public void Attach()
        {
            _commitHandler = Commit;
            _selectionHandler = (sender, args) => Commit(sender, args);
            editor.AddHandler(UIElement.LostFocusEvent, _commitHandler, true);
            editor.AddHandler(ButtonBase.ClickEvent, _commitHandler, true);
            editor.AddHandler(Selector.SelectionChangedEvent, _selectionHandler, true);
        }
        public void Detach()
        {
            if (_commitHandler == null) return;
            editor.RemoveHandler(UIElement.LostFocusEvent, _commitHandler);
            editor.RemoveHandler(ButtonBase.ClickEvent, _commitHandler);
            editor.RemoveHandler(Selector.SelectionChangedEvent, _selectionHandler!);
        }
        private void Commit(object sender, RoutedEventArgs args)
        {
            // Loading bindings is not a user edit. A host command may synchronously
            // refresh the same cached editor and raise selection/focus events again.
            if (!editor.IsLoaded || _committing) return;
            try { _committing = true; flush(); }
            catch (Exception ex)
            {
                // The scheduler retains the failed save. Do not turn a routed UI
                // event into an unhandled dispatcher exception or report success.
                Trace.TraceError($"编辑器提交未完成，保留待保存参数：{ex}");
            }
            finally { _committing = false; }
        }
    }
}
