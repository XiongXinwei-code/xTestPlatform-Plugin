using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using MessagePack;

namespace LabVIEWCallPlugin.Models
{
    /// <summary>
    /// LabVIEW Call 步骤设置数据模型。
    /// 使用 MessagePack 序列化后存储在 Step.StepSetting.Setting (byte[]) 中。
    /// </summary>
    [MessagePackObject(true)]
    public class LabVIEWCallSetting : INotifyPropertyChanged
    {
        private string _viFilePath = string.Empty;
        private bool _closePanel;
        private bool _showPanel;
        private string _inputParameters = string.Empty;
        private string _outputParameters = string.Empty;

        /// <summary>VI 文件完整路径</summary>
        public string ViFilePath
        {
            get => _viFilePath;
            set => SetProperty(ref _viFilePath, value);
        }

        /// <summary>执行完毕后关闭前面板</summary>
        public bool ClosePanel
        {
            get => _closePanel;
            set => SetProperty(ref _closePanel, value);
        }

        /// <summary>调用时显示前面板</summary>
        public bool ShowPanel
        {
            get => _showPanel;
            set => SetProperty(ref _showPanel, value);
        }

        /// <summary>
        /// VI 输入控件（Control）参数的 JSON 序列化字符串，
        /// 对应 LvPanelView 中的 ControlXML / ControlNodes。
        /// </summary>
        public string InputParameters
        {
            get => _inputParameters;
            set => SetProperty(ref _inputParameters, value);
        }

        /// <summary>
        /// VI 输出指示器（Indicator）参数的 JSON 序列化字符串，
        /// 对应 LvPanelView 中的 IndicatorXML / IndicatorNodes。
        /// </summary>
        public string OutputParameters
        {
            get => _outputParameters;
            set => SetProperty(ref _outputParameters, value);
        }

        // ── INotifyPropertyChanged ────────────────────────────────────
        public event PropertyChangedEventHandler? PropertyChanged;

        protected bool SetProperty<T>(ref T storage, T value,
            [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value))
                return false;
            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}