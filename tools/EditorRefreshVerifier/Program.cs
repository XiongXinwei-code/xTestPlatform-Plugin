using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;

namespace EditorRefreshVerifier;

/// <summary>
/// Uses the deployed SDK and actual editor descriptors on one STA. The injected
/// command is a contract simulation; this tool is not the platform application.
/// It never creates an executor, evaluates expressions or invokes a device step.
/// </summary>
internal static class Program
{
    private static readonly string[] CandidateNames =
    [
        "ConnectionName", "TaskName", "AxisName", "ClientName", "SessionName", "ServiceName", "PortName",
        "ResultVariable", "TargetVariable", "ResponseVariable", "StatusVariable", "SourceJson", "SourceXml",
        "TimeoutMs", "SendTimeoutMs", "ReadTimeoutMs", "ReplaceTimeoutMs", "PollIntervalMs", "Channels", "Channel", "PhysicalChannels"
    ];

    [STAThread]
    private static int Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        if (!TryParseArguments(args, out args, out string? hostDirectory))
        {
            Console.Error.WriteLine("用法: EditorRefreshVerifier <单个插件产物目录> [JSON报告路径] [StepTypeId过滤] [--host-factory <平台安装目录>]");
            return 1;
        }

        var directory = Path.GetFullPath(args[0]);
        var steps = new List<StepResult>(); var errors = new List<string>();
        string? primary = null, identity = null, mvid = null, hash = null;
        HostStepFactory? hostFactory = null;
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var dispatcherErrors = new List<Exception>();
        app.DispatcherUnhandledException += (_, e) => { dispatcherErrors.Add(e.Exception); e.Handled = true; };
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        try
        {
            primary = Path.Combine(directory, new DirectoryInfo(directory).Name + ".StepPlugin.dll");
            if (!File.Exists(primary))
            {
                string[] candidates = Directory.GetFiles(directory, "*.StepPlugin.dll");
                if (candidates.Length != 1) throw new InvalidOperationException("无法唯一确定主插件程序集。");
                primary = candidates[0];
            }
            string uiPath = primary.Replace(".StepPlugin.dll", ".StepPlugin.UI.dll", StringComparison.Ordinal);
            if (!File.Exists(uiPath)) throw new FileNotFoundException("缺少 UI 插件程序集。", uiPath);
            var resolver = new AssemblyDependencyResolver(uiPath);
            AssemblyLoadContext.Default.Resolving += (_, name) =>
            {
                string? path = null;
                if (hostDirectory != null)
                {
                    string hostDependency = Path.Combine(hostDirectory, name.Name + ".dll");
                    if (File.Exists(hostDependency)) path = hostDependency;
                }
                path ??= resolver.ResolveAssemblyToPath(name);
                if (path == null)
                {
                    string candidate = Path.Combine(directory, name.Name + ".dll");
                    if (File.Exists(candidate)) path = candidate;
                }
                return path == null ? null : AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
            };
            // The installed factory and its SDK contract must load before candidate plugins.
            // Do not repair the StepSetting metadata produced by this factory.
            if (hostDirectory != null) hostFactory = new HostStepFactory(hostDirectory);
            var core = AssemblyLoadContext.Default.LoadFromAssemblyPath(primary);
            var ui = AssemblyLoadContext.Default.LoadFromAssemblyPath(uiPath);
            identity = core.FullName; mvid = core.ManifestModule.ModuleVersionId.ToString();
            hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(primary)));
            var plugins = ConcreteImplementations(core, "IStepPlugin")
                .Select(type => Activator.CreateInstance(type)!)
                .ToDictionary(plugin => (string)Get(plugin, "StepTypeId"), StringComparer.Ordinal);
            var editors = ConcreteImplementations(ui, "IStepEditorPlugin")
                .Select(type => Activator.CreateInstance(type)!)
                .OrderBy(editor => (string)Get(editor, "StepTypeId"), StringComparer.Ordinal).ToArray();
            foreach (var editor in editors)
            {
                string id = (string)Get(editor, "StepTypeId");
                if (args.Length == 3 && id != args[2]) continue;
                Console.Error.WriteLine($"检查编辑器 {id}");
                if (!plugins.TryGetValue(id, out var plugin))
                {
                    steps.Add(new() { StepTypeId = id, EditorType = editor.GetType().FullName!, Error = "找不到对应核心步骤描述器。" });
                    continue;
                }
                steps.Add(VerifyEditor(plugin, editor, dispatcherErrors, hostFactory));
            }
            if (steps.Count == 0) errors.Add("没有匹配的编辑器。");
        }
        catch (Exception ex) { errors.Add(Unwrap(ex).ToString()); }
        finally { app.Shutdown(); }

        var report = new
        {
            Kind = "OfflineEditorHostContractSimulation",
            PluginDirectory = directory, AssemblyPath = primary, Assembly = identity, Mvid = mvid, Sha256 = hash,
            StepCreation = hostFactory == null ? "SyntheticWithSerializerMetadata" : "InstalledStepFactory",
            HostFactory = hostFactory?.Identity,
            Total = steps.Count, Passed = steps.Count(s => s.Passed), Failed = steps.Count(s => !s.Passed), Steps = steps, Errors = errors,
            Checks = new[] { "真实编辑器创建并在同一 STA 运行", "宿主注入 ExecuteCommand 后参数保存触发命令", "命令后通过真实 GenerateDescription 生成描述", "保存命令内重入 RefreshFromStep 后再次编辑", "刷新/卸载时提交待保存编辑" },
            Limitations = new[] { "宿主命令为手册接口模拟，未运行真实 xTestPlatform 序列列表", "通过 VM 属性修改和保存，不等同于人工点击全部控件", "只测试每个编辑器的一个安全标量及两轮编辑，未穷尽全部参数组合", "未创建执行器、求值表达式、连接或控制设备" }
        };
        string json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        if (args.Length >= 2)
        {
            string output = Path.GetFullPath(args[1]); Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            File.WriteAllText(output, json, new UTF8Encoding(false));
        }
        else Console.WriteLine(json);
        Console.Error.WriteLine($"编辑器验证：{report.Passed}/{report.Total} 通过，{report.Failed} 失败，程序集错误 {errors.Count}。");
        return errors.Count == 0 && steps.All(s => s.Passed) ? 0 : 2;
    }

    private static StepResult VerifyEditor(object plugin, object editor, List<Exception> dispatcherErrors, HostStepFactory? hostFactory)
    {
        var result = new StepResult { StepTypeId = (string)Get(editor, "StepTypeId"), EditorType = editor.GetType().FullName! };
        FrameworkElement? view = null;
        int oldDispatcherErrors = dispatcherErrors.Count;
        try
        {
            Type editorContract = Interface(editor, "IStepEditorPlugin");
            MethodInfo create = editorContract.GetMethod("CreateEditor")!;
            object serializer = Invoke(plugin, "CreateSerializer");
            Type serializerContract = Interface(serializer, "IStepSettingSerializer");
            object setting = serializerContract.GetMethod("CreateDefault")!.Invoke(serializer, null)!;
            int version = (int)serializerContract.GetProperty("SettingVersion")!.GetValue(serializer)!;
            object step = hostFactory?.CreateStep(plugin) ?? Activator.CreateInstance(create.GetParameters()[0].ParameterType)!;
            object stepSetting = Get(step, "StepSetting");
            object general = Get(Get(step, "PropertiesSetting"), "General");
            if (hostFactory == null)
            {
                byte[] bytes = (byte[])serializerContract.GetMethod("Serialize")!.Invoke(serializer, [setting])!;
                Set(stepSetting, "StepType", result.StepTypeId); Set(stepSetting, "SettingVersion", version); Set(stepSetting, "Setting", bytes);
            }
            result.InitialSettingVersion = (int)Get(stepSetting, "SettingVersion");
            result.SerializerSettingVersion = version;
            string Description() => (string)Invoke(plugin, "GenerateDescription", Get(stepSetting, "Setting"));
            if (hostFactory == null) Set(general, "StepDescription", Description());
            result.InitialDescription = (string)Get(general, "StepDescription");
            view = (FrameworkElement)create.Invoke(editor, [step, null])!;
            result.ViewType = view.GetType().FullName!;
            view.Measure(new Size(1000, 720)); view.Arrange(new Rect(0, 0, 1000, 720)); view.UpdateLayout();
            object model = view.GetType().GetProperty("ViewModel")?.GetValue(view) ?? view.DataContext
                ?? throw new InvalidOperationException("编辑器未暴露 ViewModel/DataContext。");
            MethodInfo flush = model.GetType().GetMethod("FlushPendingSave", Type.EmptyTypes)
                ?? model.GetType().GetMethod("Flush", Type.EmptyTypes)
                ?? throw new InvalidOperationException("编辑器 VM 无可调用的 FlushPendingSave/Flush。");
            MethodInfo refresh = view.GetType().GetMethod("RefreshFromStep")
                ?? throw new InvalidOperationException("编辑器未公开 RefreshFromStep。");
            PropertyInfo commandProperty = view.GetType().GetProperty("ExecuteCommand")
                ?? throw new InvalidOperationException("编辑器未暴露宿主 ExecuteCommand 注入属性。");
            bool refreshInsideCommand = false;
            Action<string, Action> command = (_, apply) =>
            {
                result.CommandCalls++;
                apply();
                Set(general, "StepDescription", Description());
                if (refreshInsideCommand) { refresh.Invoke(view, [step]); result.ReentrantRefreshes++; }
            };
            commandProperty.SetValue(view, command);
            PropertyInfo property = CandidateNames.Select(name => model.GetType().GetProperty(name))
                .FirstOrDefault(p => p is { CanRead: true, CanWrite: true } && p.SetMethod?.IsPublic == true &&
                    (p.PropertyType == typeof(string) || p.PropertyType == typeof(int)) &&
                    setting.GetType().GetProperty(p.Name) is { CanRead: true, CanWrite: true } source && source.PropertyType == p.PropertyType)
                ?? throw new InvalidOperationException("找不到具有相同设置字段的安全标量；请为此页面补充定向验证。");
            PropertyInfo storedProperty = setting.GetType().GetProperty(property.Name)!;
            result.EditedProperty = property.Name;
            for (int round = 1; round <= 2; round++)
            {
                refreshInsideCommand = round == 2;
                byte[] previous = ((byte[])Get(stepSetting, "Setting")).ToArray();
                int previousCalls = result.CommandCalls;
                object value = Replacement(storedProperty, property.GetValue(model), round);
                property.SetValue(model, value);
                flush.Invoke(model, null);
                byte[] saved = (byte[])Get(stepSetting, "Setting");
                if (previous.SequenceEqual(saved)) throw new InvalidOperationException($"第 {round} 轮 {property.Name} 编辑后设置字节未变化。");
                if (result.CommandCalls <= previousCalls) throw new InvalidOperationException($"第 {round} 轮保存未经过宿主 ExecuteCommand。");
                object restored = serializerContract.GetMethod("Deserialize")!.Invoke(serializer, [saved, Get(stepSetting, "SettingVersion")])!;
                if (!Equals(value, storedProperty.GetValue(restored))) throw new InvalidOperationException($"第 {round} 轮保存值与输入不一致。");
                if (!Equals(value, property.GetValue(model))) throw new InvalidOperationException($"第 {round} 轮保存/重入刷新后 VM 被旧值覆盖。");
                if (!Equals(Description(), Get(general, "StepDescription"))) throw new InvalidOperationException($"第 {round} 轮描述与保存参数不一致。");
                result.CompletedEdits++;
            }
            view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
            if (dispatcherErrors.Count > oldDispatcherErrors) throw new AggregateException(dispatcherErrors.Skip(oldDispatcherErrors));
            result.FinalDescription = (string)Get(general, "StepDescription");
            result.DescriptionChanged = result.InitialDescription != result.FinalDescription;
            result.Passed = result.CompletedEdits == 2 && result.ReentrantRefreshes >= 1;
        }
        catch (Exception ex) { result.Error = Unwrap(ex).ToString(); }
        finally
        {
            if (view != null)
            {
                try { view.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent)); }
                catch (Exception ex) { result.Passed = false; result.Error += "\n卸载异常：" + Unwrap(ex); }
            }
        }
        return result;
    }

    private static bool TryParseArguments(string[] input, out string[] positional, out string? hostDirectory)
    {
        var values = new List<string>();
        hostDirectory = null;
        for (int i = 0; i < input.Length; i++)
        {
            if (input[i] == "--host-factory")
            {
                if (hostDirectory != null || ++i >= input.Length || input[i].StartsWith("--", StringComparison.Ordinal))
                { positional = []; return false; }
                hostDirectory = Path.GetFullPath(input[i]);
            }
            else
            {
                if (input[i].StartsWith("--", StringComparison.Ordinal)) { positional = []; return false; }
                values.Add(input[i]);
            }
        }
        positional = values.ToArray();
        return positional.Length is >= 1 and <= 3 && Directory.Exists(positional[0]) &&
            (hostDirectory == null || File.Exists(Path.Combine(hostDirectory, "xTestPlatform.Core.dll")));
    }

    /// <summary>Creates detached steps only; no project, application service or executor is started.</summary>
    private sealed class HostStepFactory
    {
        private readonly object _factory;
        private readonly MethodInfo _create;
        public object Identity { get; }

        public HostStepFactory(string directory)
        {
            string path = Path.Combine(directory, "xTestPlatform.Core.dll");
            Assembly core = AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
            Type factoryType = core.GetType("xTestPlatform.Core.Services.StepFactory", throwOnError: true)!;
            object addresses = Activator.CreateInstance(core.GetType("xTestPlatform.Core.Utils.StepAddressGenerator", throwOnError: true)!)!;
            object registry = Activator.CreateInstance(core.GetType("xTestPlatform.Core.Plugins.StepPluginRegistry", throwOnError: true)!)!;
            _factory = Activator.CreateInstance(factoryType, addresses, registry)!;
            _create = factoryType.GetMethod("CreateStepFromPlugin", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingMethodException(factoryType.FullName, "CreateStepFromPlugin");
            Assembly sdk = _create.GetParameters()[0].ParameterType.Assembly;
            Identity = new
            {
                InstallationDirectory = directory,
                AssemblyPath = path, Assembly = core.FullName,
                Mvid = core.ManifestModule.ModuleVersionId.ToString(),
                Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))),
                SdkAssemblyPath = sdk.Location, SdkAssembly = sdk.FullName,
                SdkMvid = sdk.ManifestModule.ModuleVersionId.ToString(),
                FactoryMethod = _create.ToString()
            };
        }

        public object CreateStep(object plugin) => _create.Invoke(_factory, [plugin, "OfflineEditorFactoryProbe"])!;
    }

    private static object Replacement(PropertyInfo property, object? previous, int round)
    {
        if (property.PropertyType == typeof(int)) return Math.Max(1, (int?)previous ?? 0) + 7;
        bool expression = property.CustomAttributes.Any(a => a.AttributeType.Name == "ExpressionFieldAttribute");
        bool variable = property.CustomAttributes.Any(a => a.AttributeType.Name == "VariablePathFieldAttribute") || property.Name.EndsWith("Variable", StringComparison.Ordinal);
        string value = variable ? $"Locals.EditorRefreshProbe{round}" : $"EditorRefreshProbe{round}";
        return expression ? JsonSerializer.Serialize(value) : value;
    }

    private static IEnumerable<Type> ConcreteImplementations(Assembly assembly, string contract) => assembly.GetTypes()
        .Where(t => t.IsClass && !t.IsAbstract && t.GetInterfaces().Any(i => i.Name == contract));
    private static Type Interface(object value, string name) => value.GetType().GetInterfaces().Single(i => i.Name == name);
    private static object Get(object value, string name) => value.GetType().GetProperty(name)!.GetValue(value)!;
    private static void Set(object value, string name, object replacement) => value.GetType().GetProperty(name)!.SetValue(value, replacement);
    private static object Invoke(object value, string method, params object[] parameters) => value.GetType().GetMethod(method)!.Invoke(value, parameters)!;
    private static Exception Unwrap(Exception ex) => ex is TargetInvocationException { InnerException: { } inner } ? Unwrap(inner) : ex;
}

internal sealed class StepResult
{
    public string StepTypeId { get; init; } = "";
    public string EditorType { get; init; } = "";
    public string ViewType { get; set; } = "";
    public string EditedProperty { get; set; } = "";
    public int? InitialSettingVersion { get; set; }
    public int? SerializerSettingVersion { get; set; }
    public int CommandCalls { get; set; }
    public int ReentrantRefreshes { get; set; }
    public int CompletedEdits { get; set; }
    public string InitialDescription { get; set; } = "";
    public string FinalDescription { get; set; } = "";
    public bool DescriptionChanged { get; set; }
    public bool Passed { get; set; }
    public string? Error { get; set; }
}
