using Godot;
using System;
using System.IO;

namespace PlanetGeneration.Rendering;

/// <summary>
/// 统一读取 res:// 文本资源，兼容编辑器运行、导出运行和纯本地开发环境。
/// 资源目录探测与异常日志集中在这里，目录清单类只负责解析 JSON。
/// </summary>
internal static class AssetTextLoader
{
    public static bool TryRead(
        string resourcePath,
        out string content,
        params string[] fallbackPaths)
    {
        content = string.Empty;
        var globalPath = ProjectSettings.GlobalizePath(resourcePath);

        if (TryReadFile(globalPath, resourcePath, out content))
        {
            return true;
        }

        if (Godot.FileAccess.FileExists(resourcePath))
        {
            try
            {
                using var file = Godot.FileAccess.Open(resourcePath, Godot.FileAccess.ModeFlags.Read);
                if (file != null)
                {
                    content = file.GetAsText();
                    if (!string.IsNullOrEmpty(content)) return true;
                }
                else
                {
                    GD.PushWarning($"[AssetTextLoader] 无法打开资源: {resourcePath}; {Godot.FileAccess.GetOpenError()}");
                }
            }
            catch (Exception ex)
            {
                GD.PushWarning($"[AssetTextLoader] 读取资源失败: {resourcePath}; {ex.Message}");
            }
        }

        foreach (var fallbackPath in fallbackPaths)
        {
            if (TryReadFile(fallbackPath, resourcePath, out content))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryReadFile(string path, string resourcePath, out string content)
    {
        content = string.Empty;
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            content = File.ReadAllText(path);
            return !string.IsNullOrEmpty(content);
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[AssetTextLoader] 读取文件失败: {path} ({resourcePath}); {ex.Message}");
            return false;
        }
    }
}
