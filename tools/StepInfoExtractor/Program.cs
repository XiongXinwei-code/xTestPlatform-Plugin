// StepInfoExtractor：反射扫描插件目录中的 *.StepPlugin.dll，
// 提取所有 IStepPlugin 实现的 StepTypeId、DisplayName 与 Description（简述），
// 以 JSON 数组输出到 stdout：[{ "stepTypeId": "...", "displayName": "...", "description": "..." }]
// stepTypeId 是序列文件中实际存储的步骤类型标识，客户端据此由未知步骤反查所需插件，
// 不可省略；DisplayName 仅用于展示，与 stepTypeId 无固定对应关系。
// 用法：StepInfoExtractor <插件产物目录>
// 警告信息输出到 stderr，单个类型失败不影响整体（跳过并警告）。

using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;

Console.OutputEncoding = Encoding.UTF8;

if (args.Length < 1 || !Directory.Exists(args[0]))
{
    Console.Error.WriteLine("用法: StepInfoExtractor <插件产物目录>");
    return 1;
}

var pluginDir = Path.GetFullPath(args[0]);
var steps = new List<StepInfo>();
var editorIds = new List<string>();
var strict = args.Skip(1).Contains("--strict", StringComparer.Ordinal);
var invalid = false;

// 有同名主程序集时，只提取本插件步骤，其他 StepPlugin.dll 作为私有依赖加载。
var primaryAssembly = Path.Combine(pluginDir, new DirectoryInfo(pluginDir).Name + ".StepPlugin.dll");
var dlls = File.Exists(primaryAssembly) ? new[] { primaryAssembly } : Directory.GetFiles(pluginDir, "*.StepPlugin.dll");
if (dlls.Length == 0)
{
    invalid = true;
    Console.Error.WriteLine($"警告: {pluginDir} 中未找到 *.StepPlugin.dll");
}

foreach (var dll in dlls)
{
    var alc = new PluginLoadContext(dll);
    try
    {
        var asm = alc.LoadFromAssemblyPath(dll);
        Type[] types;
        try
        {
            types = asm.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            invalid = true;
            types = ex.Types.Where(t => t is not null).Cast<Type>().ToArray();
            foreach (var le in ex.LoaderExceptions.Where(e => e is not null).Take(3))
                Console.Error.WriteLine($"警告: {Path.GetFileName(dll)} 部分类型加载失败: {le!.Message}");
        }

        foreach (var type in types)
        {
            if (type.IsAbstract || !type.IsClass) continue;
            if (!type.GetInterfaces().Any(i => i.Name == "IStepPlugin")) continue;

            try
            {
                var instance = Activator.CreateInstance(type);
                var stepTypeId = GetStringProperty(type, instance, "StepTypeId");
                var displayName = GetStringProperty(type, instance, "DisplayName");
                var description = GetStringProperty(type, instance, "Description");
                if (strict)
                {
                    // Creating an executor must not open hardware. Validate the
                    // actual inherited factories too (e.g. six CANopen cycle steps).
                    var factory = type.GetMethod("CreateExecutor", Type.EmptyTypes)
                        ?? throw new InvalidOperationException($"步骤缺少执行器工厂：{type.FullName}");
                    var first = factory.Invoke(instance, null);
                    var second = factory.Invoke(instance, null);
                    if (first is null || second is null || ReferenceEquals(first, second) ||
                        first.GetType().FullName != "PluginCommon.ExecutionScopedExecutor" ||
                        second.GetType().FullName != "PluginCommon.ExecutionScopedExecutor")
                    {
                        invalid = true;
                        Console.Error.WriteLine($"步骤工厂未返回独立的执行作用域观察器：{type.FullName}");
                    }
                }
                if (strict && string.IsNullOrWhiteSpace(description))
                {
                    invalid = true;
                    Console.Error.WriteLine($"步骤 Description 为空: {type.FullName}");
                }
                if (string.IsNullOrWhiteSpace(displayName))
                {
                    invalid = true;
                    Console.Error.WriteLine($"警告: {type.FullName} 的 DisplayName 为空，已跳过");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(stepTypeId))
                {
                    invalid = true;
                    Console.Error.WriteLine($"警告: {type.FullName} 的 StepTypeId 为空，该步骤无法被反查定位");
                }
                steps.Add(new StepInfo(stepTypeId, displayName, ShortenDescription(description)));
            }
            catch (Exception ex)
            {
                invalid = true;
                Console.Error.WriteLine($"警告: 无法实例化 {type.FullName}: {ex.GetBaseException().Message}");
            }
        }
    }
    catch (Exception ex)
    {
        invalid = true;
        Console.Error.WriteLine($"警告: 无法加载 {Path.GetFileName(dll)}: {ex.GetBaseException().Message}");
    }
}

if (strict)
{
    foreach (var ui in Directory.GetFiles(pluginDir, "*.StepPlugin.UI.dll"))
    {
        try
        {
            var context = new PluginLoadContext(ui);
            var assembly = context.LoadFromAssemblyPath(ui);
            // Only instantiate editor descriptors, never views or device executors.
            foreach (var type in assembly.GetTypes())
            {
                if (type.IsAbstract || !type.IsClass || !type.GetInterfaces().Any(i => i.Name == "IStepEditorPlugin")) continue;
                var editor = Activator.CreateInstance(type);
                var id = GetStringProperty(type, editor, "StepTypeId");
                var icon = GetStringProperty(type, editor, "IconPath");
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(icon))
                {
                    invalid = true;
                    Console.Error.WriteLine($"编辑器 StepTypeId/IconPath 为空: {type.FullName}");
                }
                editorIds.Add(id);
            }
        }
        catch (Exception ex)
        {
            invalid = true;
            Console.Error.WriteLine($"UI 类型/依赖加载失败: {Path.GetFileName(ui)}: {ex.GetBaseException().Message}");
        }
    }
    if (steps.Count == 0 || steps.GroupBy(s => s.stepTypeId, StringComparer.Ordinal).Any(g => g.Count() > 1))
        invalid = true;
    foreach (var step in steps)
    {
        var count = editorIds.Count(id => string.Equals(id, step.stepTypeId, StringComparison.Ordinal));
        if (count != 1)
        {
            invalid = true;
            Console.Error.WriteLine($"步骤 {step.stepTypeId} 需要唯一匹配的 IStepEditorPlugin，实际={count}");
        }
    }
}

var json = JsonSerializer.Serialize(
    steps.OrderBy(s => s.displayName, StringComparer.Ordinal),
    new JsonSerializerOptions
    {
        WriteIndented = true,
        // Metadata is a machine protocol. ASCII escapes also remain valid under legacy Windows code pages.
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Default
    });
Console.WriteLine(json);
return strict && invalid ? 2 : 0;

// 读取实例的 string 属性（含继承链）
static string GetStringProperty(Type type, object? instance, string name)
{
    var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
    return prop?.GetValue(instance) as string ?? string.Empty;
}

// Description 为多行 Markdown（## 功能 / ## 参数 …），提取"## 功能"段的第一行非空文本作为简述
static string ShortenDescription(string description)
{
    if (string.IsNullOrWhiteSpace(description)) return string.Empty;
    var lines = description.Replace("\r\n", "\n").Split('\n');
    var inFunc = false;
    foreach (var raw in lines)
    {
        var line = raw.Trim();
        if (line.StartsWith("##") && line.Contains("功能")) { inFunc = true; continue; }
        if (inFunc)
        {
            if (line.StartsWith("##")) break;
            if (line.Length > 0) return line;
        }
    }
    // 无"## 功能"章节时取第一行非标题文本
    return lines.Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0 && !l.StartsWith("#")) ?? string.Empty;
}

internal sealed record StepInfo(string stepTypeId, string displayName, string description);

// 从插件目录探测依赖，避免加载失败
internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    private readonly string _pluginDir;

    public PluginLoadContext(string mainAssemblyPath) : base(isCollectible: false)
    {
        _resolver = new AssemblyDependencyResolver(mainAssemblyPath);
        _pluginDir = Path.GetDirectoryName(mainAssemblyPath)!;
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        if (path is null)
        {
            var candidate = Path.Combine(_pluginDir, assemblyName.Name + ".dll");
            if (File.Exists(candidate)) path = candidate;
        }
        return path is null ? null : LoadFromAssemblyPath(path);
    }

    protected override nint LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is null ? nint.Zero : LoadUnmanagedDllFromPath(path);
    }
}
