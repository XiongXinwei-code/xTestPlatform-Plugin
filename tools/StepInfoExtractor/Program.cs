// StepInfoExtractor：反射扫描插件目录中的 *.StepPlugin.dll，
// 提取所有 IStepPlugin 实现的 DisplayName 与 Description（简述），
// 以 JSON 数组输出到 stdout：[{ "displayName": "...", "description": "..." }]
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

var dlls = Directory.GetFiles(pluginDir, "*.StepPlugin.dll");
if (dlls.Length == 0)
{
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
                var displayName = GetStringProperty(type, instance, "DisplayName");
                var description = GetStringProperty(type, instance, "Description");
                if (string.IsNullOrWhiteSpace(displayName))
                {
                    Console.Error.WriteLine($"警告: {type.FullName} 的 DisplayName 为空，已跳过");
                    continue;
                }
                steps.Add(new StepInfo(displayName, ShortenDescription(description)));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"警告: 无法实例化 {type.FullName}: {ex.GetBaseException().Message}");
            }
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"警告: 无法加载 {Path.GetFileName(dll)}: {ex.GetBaseException().Message}");
    }
}

var json = JsonSerializer.Serialize(
    steps.OrderBy(s => s.displayName, StringComparer.Ordinal),
    new JsonSerializerOptions
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    });
Console.WriteLine(json);
return 0;

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

internal sealed record StepInfo(string displayName, string description);

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
