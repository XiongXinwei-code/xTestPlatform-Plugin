using System.ComponentModel;
using System.Runtime.CompilerServices;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace NiDaq.UI.ViewModels;

public abstract class AoViewModelBase<T> : INotifyPropertyChanged where T : class, new()
{
    private Step? _step;
    private IStepSettingSerializer? _serializer;
    private PluginEditor.SettingsChangeTracker? _changes;
    private readonly PluginCommon.EditorSaveScheduler _saves = new();
    protected T Setting { get; private set; } = new();
    public void AttachSerializer(IStepSettingSerializer serializer) { FlushPendingSave(); _serializer = serializer; if (_step != null) Load(); }
    public void AttachStep(Step step) { FlushPendingSave(); _step = step; Load(); }
    private void Load()
    {
        if (_serializer == null || _step == null) return;
        _changes?.Dispose();
        Setting = _step.StepSetting.Setting is { Length: > 0 } data
            ? (T)_serializer.Deserialize(data, _step.StepSetting.SettingVersion) : (T)_serializer.CreateDefault();
        _changes = new PluginEditor.SettingsChangeTracker(Save);
        TrackCollections(_changes.Track);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(""));
    }
    protected virtual void TrackCollections(Action<System.Collections.IEnumerable> track) { }
    protected void Change(Action update, [CallerMemberName] string? property = null)
    {
        update();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        Save();
    }
    private void Save() { if (_step != null && _serializer != null) _saves.Schedule(_step, _serializer, Setting); }
    public void FlushPendingSave() => _saves.Flush();
    public Exception? LastSaveError => _saves.LastError;
    public event PropertyChangedEventHandler? PropertyChanged;
}
