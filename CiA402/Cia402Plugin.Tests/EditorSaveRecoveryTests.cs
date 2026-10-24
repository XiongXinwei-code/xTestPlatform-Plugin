using System.Reflection;
using System.Windows;
using CiA402.UI.Views;
using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;
using Xunit;

namespace CiA402.Tests;

public sealed class EditorSaveRecoveryTests
{
    [StaFact]
    public void EveryActualEditorRetainsFailedSaveForRetryAndFlushesOnUnload()
    {
        string[] names = ["BindAxis", "UnbindAxis", "Enable", "Disable", "ResetFault", "SetMode", "ReadMode", "ReadState",
            "ReadFeedback", "ReadDiagnostics", "MovePosition", "RunVelocity", "RunTorque", "Home", "StopMotion",
            "WriteCspTarget", "WriteCsvTarget", "WriteCstTarget"];
        foreach (string name in names)
        {
            var plugin = (IStepPlugin)Activator.CreateInstance(typeof(BindAxisPlugin).Assembly.GetType($"CiA402.{name}Plugin")!)!;
            var serializer = plugin.CreateSerializer();
            Step NewStep()
            {
                var step = new Step(); step.StepSetting.StepType = plugin.StepTypeId;
                step.StepSetting.Setting = serializer.Serialize(serializer.CreateDefault()); step.StepSetting.SettingVersion = serializer.SettingVersion;
                return step;
            }
            var first = NewStep(); var second = NewStep();
            var view = (FrameworkElement)Activator.CreateInstance(typeof(BindAxisEditorView).Assembly.GetType($"CiA402.UI.Views.{name}EditorView")!)!;
            var refresh = view.GetType().GetMethod("RefreshFromStep")!;
            refresh.Invoke(view, [first]);
            var model = view.GetType().GetProperty("ViewModel")!.GetValue(view)!;
            var type = model.GetType(); var flush = type.GetMethod("Flush")!; var timeout = type.GetProperty("TimeoutMs")!;
            var command = type.GetProperty("ExecuteCommand")!;
            int original = (int)timeout.GetValue(model)!;
            command.SetValue(model, (Action<string, Action>)((_, _) => throw new InvalidOperationException("fake command failure")));
            timeout.SetValue(model, original + 111); flush.Invoke(model, null);
            Assert.Contains("保存设置失败", type.GetProperty("Error")!.GetValue(model)!.ToString());
            // Failed flush must not silently replace the old model with a different step.
            Assert.Throws<TargetInvocationException>(() => refresh.Invoke(view, [second]));
            command.SetValue(model, (Action<string, Action>)((_, save) => save()));
            flush.Invoke(model, null);
            var saved = serializer.Deserialize(first.StepSetting.Setting, first.StepSetting.SettingVersion);
            Assert.Equal(original + 111, (int)saved.GetType().GetProperty("TimeoutMs")!.GetValue(saved)!);
            refresh.Invoke(view, [second]); timeout.SetValue(model, original + 222);
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            saved = serializer.Deserialize(second.StepSetting.Setting, second.StepSetting.SettingVersion);
            Assert.Equal(original + 222, (int)saved.GetType().GetProperty("TimeoutMs")!.GetValue(saved)!);
            saved = serializer.Deserialize(first.StepSetting.Setting, first.StepSetting.SettingVersion);
            Assert.Equal(original + 111, (int)saved.GetType().GetProperty("TimeoutMs")!.GetValue(saved)!);
        }
    }
}
