using System.Diagnostics;
using System.Windows.Threading;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace PluginCommon;

/// <summary>手册§11.5的Dispatcher防抖保存；刷新和卸载前Flush，绝不在后台访问可变设置。</summary>
internal sealed class EditorSaveScheduler
{
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private readonly DispatcherTimer _timer;
    private Action? _pending;
    public Exception? LastError { get; private set; }
    // The host wraps this command with undo tracking, GenerateDescription and
    // sequence-list refresh. Never write framework-owned PropertiesSetting here.
    public Action<string, Action>? ExecuteCommand { get; set; }

    public EditorSaveScheduler()
    {
        _timer = new(DispatcherPriority.DataBind, _dispatcher) { Interval = TimeSpan.FromMilliseconds(200) };
        _timer.Tick += (_, _) =>
        {
            try { Flush(); }
            catch (Exception ex) { Trace.TraceError($"编辑器防抖保存失败，保留待保存配置：{ex}"); }
        };
    }

    public void Schedule(Step step, IStepSettingSerializer serializer, object setting) =>
        Queue(() =>
        {
            step.StepSetting.Setting = serializer.Serialize(setting);
            step.StepSetting.SettingVersion = serializer.SettingVersion;
        });

    public void Queue(Action save)
    {
        _dispatcher.VerifyAccess(); ArgumentNullException.ThrowIfNull(save);
        // A cached editor can be rebound before Flush. Keep the command belonging
        // to the same step as the queued setting, not the next injected command.
        var command = ExecuteCommand;
        _pending = command == null ? save : () => command("修改步骤参数", save);
        _timer.Stop(); _timer.Start();
    }

    public void Flush()
    {
        _dispatcher.VerifyAccess(); _timer.Stop();
        var save = _pending; _pending = null; // Publishing StepSetting may re-enter Refresh/Flush.
        if (save == null) return;
        try { save(); LastError = null; }
        catch (Exception ex) { _pending ??= save; LastError = ex; throw; }
    }
}
