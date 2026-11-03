using xTestPlatform.Core.Plugins.Contracts;
using xTestPlatform.Core.SequenceModels;

namespace NiGrpc.UI.Validation;

internal static class NiGrpcLifecycleValidator
{
    internal static void Check(SequenceFile? file, List<Step>? block, Step? current, NiGrpcSetting setting, string type, List<StepSettingError> errors)
    {
        if (file == null || block == null || current == null || type == "NiGrpc.Connect") return;
        var owner = file.Sequences.Values.FirstOrDefault(sequence => sequence.StepItems.Values.Any(steps => ReferenceEquals(steps, block)));
        if (owner == null) return;
        var all = new List<Step>();
        foreach (var section in new[] { BlockType.Setup, BlockType.Main, BlockType.Cleanup })
            if (owner.StepItems.TryGetValue(section, out var steps)) all.AddRange(steps);
        int index = all.IndexOf(current);
        var preceding = index > 0 ? all.Take(index) : [];
        bool connectionFound = false, sessionFound = false;
        foreach (var step in preceding)
        {
            if (step.StepSetting.StepType is not ("NiGrpc.Connect" or "NiGrpc.DaqOpen" or "NiGrpc.XnetOpen")) continue;
            try
            {
                var serializer = step.StepSetting.StepType switch { "NiGrpc.Connect" => new NiGrpcConnectPlugin().CreateSerializer(), "NiGrpc.DaqOpen" => new NiGrpcDaqOpenPlugin().CreateSerializer(), _ => new NiGrpcXnetOpenPlugin().CreateSerializer() };
                var old = (NiGrpcSetting)serializer.Deserialize(step.StepSetting.Setting, step.StepSetting.SettingVersion);
                if (old.ConnectionName != setting.ConnectionName) continue;
                if (step.StepSetting.StepType == "NiGrpc.Connect") connectionFound = true;
                if (old is NiGrpcSessionSetting oldSession && setting is NiGrpcSessionSetting currentSession && oldSession.SessionName == currentSession.SessionName) sessionFound = true;
            }
            catch (Exception) { /* 无法解析的历史设置不作为已找到前置条件的证据。 */ }
        }
        if (!connectionFound) errors.Add(StepSettingError.Warning("NIGRPC_LC01", "当前序列前面未找到对应的 NI gRPC 连接步骤；跨序列创建的连接请在运行时确认"));
        if ((type == "NiGrpc.CloseSession" || ((type.StartsWith("NiGrpc.Daq", StringComparison.Ordinal) || type.StartsWith("NiGrpc.Xnet", StringComparison.Ordinal)) && type is not ("NiGrpc.DaqOpen" or "NiGrpc.XnetOpen"))) && !sessionFound)
            errors.Add(StepSettingError.Warning("NIGRPC_LC02", "当前序列前面未找到对应的 NI gRPC 会话创建步骤；跨序列创建的会话请在运行时确认"));
    }
}
