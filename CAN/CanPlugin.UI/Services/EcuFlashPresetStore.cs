using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CAN.UI.Models;

namespace CAN.UI.Services;

/// <summary>
/// ECU 刷写规范预设的本地存储。预设保存在当前用户的 LocalAppData 目录下，
/// 属于编辑期辅助数据，不随测试序列文件一同分发。
/// </summary>
public static class EcuFlashPresetStore
{
    private static readonly object Sync = new();
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>预设文件的完整路径</summary>
    public static string FilePath
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "xTestPlatform", "Plugins", "CanFlash");
            return Path.Combine(dir, "ecu-presets.json");
        }
    }

    /// <summary>不存在时返回空列表；损坏或不可读时明确失败，不允许随后静默覆盖。</summary>
    public static List<EcuFlashPreset> Load() => Load(null);

    internal static List<EcuFlashPreset> Load(string? filePath)
    {
        lock (Sync)
        {
            string json;
            try { json = File.ReadAllText(filePath ?? FilePath, Encoding.UTF8); }
            catch (FileNotFoundException) { return []; }
            catch (DirectoryNotFoundException) { return []; }
            var presets = JsonSerializer.Deserialize<List<EcuFlashPreset>>(json, Options)
                ?? throw new InvalidDataException("刷写预设必须是有效的列表");
            Validate(presets);
            return presets;
        }
    }

    /// <summary>保存全部预设</summary>
    public static void Save(IEnumerable<EcuFlashPreset> presets) => Save(null, presets);

    internal static void Save(string? filePath, IEnumerable<EcuFlashPreset> presets)
    {
        lock (Sync)
        {
            var values = presets.ToList();
            Validate(values);
            var json = JsonSerializer.Serialize(values, Options);
            var path = Path.GetFullPath(filePath ?? FilePath);
            var dir = Path.GetDirectoryName(path)!;
            Directory.CreateDirectory(dir);
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, json, new UTF8Encoding(false));
                // Same-directory replacement: a failed write never truncates the old file.
                File.Move(temporary, path, overwrite: true);
            }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (Exception ex) { System.Diagnostics.Trace.TraceError($"刷写预设临时文件清理失败：{ex}"); }
            }
        }
    }

    /// <summary>新增或按名称覆盖一个预设，返回保存后的完整列表</summary>
    public static List<EcuFlashPreset> Upsert(EcuFlashPreset preset) => Upsert(null, preset);

    internal static List<EcuFlashPreset> Upsert(string? filePath, EcuFlashPreset preset)
    {
        lock (Sync)
        {
            var presets = Load(filePath);
            var index = presets.FindIndex(p => string.Equals(p.Name, preset.Name, StringComparison.OrdinalIgnoreCase));
            if (index >= 0) presets[index] = preset;
            else presets.Add(preset);
            Save(filePath, presets);
            return presets;
        }
    }

    /// <summary>按名称删除一个预设，返回保存后的完整列表</summary>
    public static List<EcuFlashPreset> Delete(string name) => Delete(null, name);

    internal static List<EcuFlashPreset> Delete(string? filePath, string name)
    {
        lock (Sync)
        {
            var presets = Load(filePath);
            presets.RemoveAll(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            Save(filePath, presets);
            return presets;
        }
    }

    private static void Validate(List<EcuFlashPreset> presets)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (presets.Any(p => p == null || string.IsNullOrWhiteSpace(p.Name) || !names.Add(p.Name)))
            throw new InvalidDataException("刷写预设包含空项、空名称或重复名称，请先修复原预设文件");
    }
}
