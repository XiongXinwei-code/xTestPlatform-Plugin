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
    public void EveryViewModelRetriesHostRefreshAfterBytesWereAlreadyApplied()
    {
        string[] names = ["BindAxis", "UnbindAxis", "Enable", "Disable", "ResetFault", "SetMode", "ReadMode", "ReadState",
            "ReadFeedback", "ReadDiagnostics", "MovePosition", "RunVelocity", "RunTorque", "Home", "StopMotion",
            "WriteCspTarget", "WriteCsvTarget", "WriteCstTarget"];
        foreach (var name in names)
        {
            var plugin = (IStepPlugin)Activator.CreateInstance(typeof(BindAxisPlugin).Assembly.GetType($"CiA402.{name}Plugin")!)!;
            var serializer = plugin.CreateSerializer(); var step = new Step();
            step.StepSetting.SettingVersion = serializer.SettingVersion;
            step.StepSetting.Setting = serializer.Serialize(serializer.CreateDefault());
            var vm = Activator.CreateInstance(typeof(BindAxisEditorView).Assembly.GetType($"CiA402.UI.ViewModels.{name}ViewModel")!)!;
            var type = vm.GetType();
            type.GetMethod("AttachSerializer")!.Invoke(vm, [serializer]);
            type.GetMethod("AttachStep")!.Invoke(vm, [step]);
            int calls = 0;
            string? refreshedDescription = null;
            type.GetProperty("ExecuteCommand")!.SetValue(vm, (Action<string, Action>)((_, save) =>
            {
                calls++;
                save();
                if (calls == 1) throw new InvalidOperationException("host refresh failed after saving bytes");
                refreshedDescription = plugin.GenerateDescription(step.StepSetting.Setting);
            }));
            var timeout = type.GetProperty("TimeoutMs")!;
            var changedTimeout = (int)timeout.GetValue(vm)! + 17;
            timeout.SetValue(vm, changedTimeout);
            var flush = type.GetMethod("Flush")!;
            flush.Invoke(vm, null);
            Assert.Contains("保存设置失败", type.GetProperty("Error")!.GetValue(vm)!.ToString());
            var saved = serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
            Assert.Equal(changedTimeout, (int)saved.GetType().GetProperty("TimeoutMs")!.GetValue(saved)!);
            Assert.Null(refreshedDescription);

            flush.Invoke(vm, null);
            Assert.Equal(2, calls);
            Assert.Equal(plugin.GenerateDescription(step.StepSetting.Setting), refreshedDescription);
            Assert.DoesNotContain("保存设置失败", type.GetProperty("Error")!.GetValue(vm)!.ToString());
        }
    }

    [StaFact]
    public void EveryViewModelCapturesHostCommandWhenTheEditBecomesDirty()
    {
        string[] names = ["BindAxis", "UnbindAxis", "Enable", "Disable", "ResetFault", "SetMode", "ReadMode", "ReadState",
            "ReadFeedback", "ReadDiagnostics", "MovePosition", "RunVelocity", "RunTorque", "Home", "StopMotion",
            "WriteCspTarget", "WriteCsvTarget", "WriteCstTarget"];
        foreach (var name in names)
        {
            var plugin = (IStepPlugin)Activator.CreateInstance(typeof(BindAxisPlugin).Assembly.GetType($"CiA402.{name}Plugin")!)!;
            var serializer = plugin.CreateSerializer(); var step = new Step();
            step.StepSetting.SettingVersion = serializer.SettingVersion;
            step.StepSetting.Setting = serializer.Serialize(serializer.CreateDefault());
            var vm = Activator.CreateInstance(typeof(BindAxisEditorView).Assembly.GetType($"CiA402.UI.ViewModels.{name}ViewModel")!)!;
            var type = vm.GetType();
            type.GetMethod("AttachSerializer")!.Invoke(vm, [serializer]);
            type.GetMethod("AttachStep")!.Invoke(vm, [step]);
            int originalCalls = 0, replacementCalls = 0;
            var command = type.GetProperty("ExecuteCommand")!;
            command.SetValue(vm, (Action<string, Action>)((_, save) => { originalCalls++; save(); }));
            var timeout = type.GetProperty("TimeoutMs")!;
            timeout.SetValue(vm, (int)timeout.GetValue(vm)! + 17);
            command.SetValue(vm, (Action<string, Action>)((_, save) => { replacementCalls++; save(); }));
            type.GetMethod("Flush")!.Invoke(vm, null);
            Assert.Equal(1, originalCalls); Assert.Equal(0, replacementCalls);
        }
    }

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
            bool failOriginalCommand = true;
            int replacementCommandCalls = 0;
            command.SetValue(model, (Action<string, Action>)((_, save) =>
            {
                if (failOriginalCommand) throw new InvalidOperationException("fake command failure");
                save();
            }));
            timeout.SetValue(model, original + 111); flush.Invoke(model, null);
            Assert.Contains("保存设置失败", type.GetProperty("Error")!.GetValue(model)!.ToString());
            // Failed flush must not silently replace the old model with a different step.
            Assert.Throws<TargetInvocationException>(() => refresh.Invoke(view, [second]));
            command.SetValue(model, (Action<string, Action>)((_, save) => { replacementCommandCalls++; save(); }));
            failOriginalCommand = false;
            flush.Invoke(model, null);
            Assert.Equal(0, replacementCommandCalls); // The failed operation still belongs to the original host command.
            var saved = serializer.Deserialize(first.StepSetting.Setting, first.StepSetting.SettingVersion);
            Assert.Equal(original + 111, (int)saved.GetType().GetProperty("TimeoutMs")!.GetValue(saved)!);
            refresh.Invoke(view, [second]); timeout.SetValue(model, original + 222);
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            Assert.Equal(1, replacementCommandCalls);
            saved = serializer.Deserialize(second.StepSetting.Setting, second.StepSetting.SettingVersion);
            Assert.Equal(original + 222, (int)saved.GetType().GetProperty("TimeoutMs")!.GetValue(saved)!);
            saved = serializer.Deserialize(first.StepSetting.Setting, first.StepSetting.SettingVersion);
            Assert.Equal(original + 111, (int)saved.GetType().GetProperty("TimeoutMs")!.GetValue(saved)!);
        }
    }
}
