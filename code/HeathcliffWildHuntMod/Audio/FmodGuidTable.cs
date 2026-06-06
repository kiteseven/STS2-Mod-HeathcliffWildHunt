using System;
using System.Collections.Generic;
using Godot;
using FileAccess = Godot.FileAccess;

namespace HeathcliffWildHuntMod.Audio;

/// <summary>
///     解析 FMOD Studio 导出的 GUIDs.txt，建立 事件路径(event:/…) → GUID 的映射表（狂猎 mod 自实现）。
///     为什么需要它：mod 的 bank 不带 strings.bank（README 核心规则：不准动 Master），
///     FMOD 运行时无法用字符串路径解析 mod 事件，必须先查表拿到 GUID，再用 play_one_shot_using_guid 播放。
///     GUIDs.txt 每行格式：<c>{GUID} event:/路径</c>（也可能是 bank:/ 或 bus:/，本表只收 event:/）。
/// </summary>
internal static class FmodGuidTable
{
    /// <summary>路径 → 带花括号 GUID 的映射；用 Ordinal 比较保证大小写敏感匹配事件路径。</summary>
    private static readonly Dictionary<string, string> PathToGuid = new(StringComparer.Ordinal);
    private static readonly object Gate = new();

    /// <summary>当前已登记的事件映射数量（诊断用）。</summary>
    public static int Count
    {
        get
        {
            lock (Gate)
            {
                return PathToGuid.Count;
            }
        }
    }

    /// <summary>
    ///     从 res:// 资源文件读取并解析 GUIDs.txt，合并进映射表；文件缺失或不可读时返回 false。
    /// </summary>
    public static bool LoadFromResource(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath) || !FileAccess.FileExists(resourcePath))
        {
            GD.PushWarning($"[WildHunt][Audio] GUIDs 文件不存在: {resourcePath}");
            return false;
        }

        using var file = FileAccess.Open(resourcePath, FileAccess.ModeFlags.Read);
        if (file is null)
        {
            GD.PushWarning($"[WildHunt][Audio] GUIDs 文件打开失败: {resourcePath}");
            return false;
        }

        Parse(file.GetAsText(), resourcePath);
        return true;
    }

    /// <summary>
    ///     逐行解析 GUIDs.txt 文本，提取 event:/ 行写入映射表。容错：跳过空行、注释、格式错误行。
    /// </summary>
    private static void Parse(string text, string sourceLabel)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var added = 0;

        lock (Gate)
        {
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.Length == 0 || line[0] == '#')
                    continue; // 空行与注释

                // 行首必须是 "{GUID}"，先定位右花括号
                var close = line.IndexOf('}');
                if (line[0] != '{' || close <= 1)
                    continue; // 格式不符，静默跳过（FMOD 导出偶有非事件行）

                var guidFragment = line.AsSpan(1, close - 1).Trim().ToString();
                if (!Guid.TryParse(guidFragment, out var parsed))
                    continue; // GUID 非法

                // 右花括号之后是路径部分
                var pathPart = close + 1 < line.Length ? line[(close + 1)..].TrimStart() : string.Empty;
                if (pathPart.Length == 0 || !pathPart.StartsWith("event:", StringComparison.Ordinal))
                    continue; // 只收 event:/ 路径，忽略 bank:/ bus:/

                PathToGuid[pathPart] = parsed.ToString("B"); // 统一存带花括号格式
                added++;
            }
        }

        GD.Print($"[WildHunt][Audio] 解析 GUIDs 完成 ({sourceLabel}): 新增/更新 {added} 条事件映射，当前共 {Count} 条。");
    }

    /// <summary>
    ///     按事件路径查映射的 GUID；命中返回 true。
    /// </summary>
    public static bool TryGetGuid(string eventPath, out string guid)
    {
        guid = string.Empty;
        if (string.IsNullOrEmpty(eventPath))
            return false;

        lock (Gate)
        {
            if (PathToGuid.TryGetValue(eventPath, out var v) && v is not null)
            {
                guid = v;
                return true;
            }
        }

        return false;
    }

    /// <summary>是否登记过该事件路径（供 NAudioManager patch 快速判断是否归本 mod 处理）。</summary>
    public static bool IsMappedPath(string eventPath)
    {
        return TryGetGuid(eventPath, out _);
    }
}
