using System.IO;
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

    /// <summary>加载全部预设，文件不存在或损坏时返回空列表</summary>
    public static List<EcuFlashPreset> Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return [];

            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<List<EcuFlashPreset>>(json, Options) ?? [];
        }
        catch
        {
            // 预设文件损坏不应阻断编辑器加载
            return [];
        }
    }

    /// <summary>保存全部预设</summary>
    public static void Save(IEnumerable<EcuFlashPreset> presets)
    {
        var path = FilePath;
        var dir = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(dir);
        File.WriteAllText(path, JsonSerializer.Serialize(presets, Options));
    }

    /// <summary>新增或按名称覆盖一个预设，返回保存后的完整列表</summary>
    public static List<EcuFlashPreset> Upsert(EcuFlashPreset preset)
    {
        var presets = Load();
        var index = presets.FindIndex(p => string.Equals(p.Name, preset.Name, StringComparison.OrdinalIgnoreCase));

        if (index >= 0)
            presets[index] = preset;
        else
            presets.Add(preset);

        Save(presets);
        return presets;
    }

    /// <summary>按名称删除一个预设，返回保存后的完整列表</summary>
    public static List<EcuFlashPreset> Delete(string name)
    {
        var presets = Load();
        presets.RemoveAll(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        Save(presets);
        return presets;
    }
}
