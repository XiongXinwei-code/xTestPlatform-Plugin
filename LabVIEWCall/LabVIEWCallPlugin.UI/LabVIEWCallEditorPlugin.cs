using LabVIEWCallPlugin.UI.Views;
using StepEditor.Abstractions;
using System.Windows;
using xTestPlatform.Core.SequenceModels;

namespace LabVIEWCallPlugin.UI
{
    /// <summary>
    /// LabVIEW Call 编辑器插件（UI 层）。
    /// 只负责创建 WPF 编辑器，步骤设置校验由 LabVIEWCallStepPlugin（运行层）实现。
    /// </summary>
    public sealed class LabVIEWCallEditorPlugin : IStepEditorPlugin
    {
        public string StepTypeId => "LabVIEWCall";

        public string IconPath =>
            "pack://application:,,,/LabVIEWCall.StepPlugin.UI;component/Resources/Icons/labview.png";

        public FrameworkElement CreateEditor(Step step, SequenceFile? sequenceFile)
            => new LabVIEWCallEditorView(step, sequenceFile);
    }
}
