using System;
using Godot;

namespace HeathcliffWildHuntMod.Audio;

/// <summary>
///     FmodServer 单例的底层访问层（狂猎 mod 自实现，不依赖任何前置库）。
///     原理：游戏的 FMOD GDExtension 在引擎启动时注册了一个名为 "FmodServer" 的 Godot 单例，
///     我们直接通过 <see cref="Engine.GetSingleton(StringName)" /> 拿到它，再用 <see cref="GodotObject.Call(StringName, Variant[])" />
///     调用其 GDExtension 方法（load_bank / play_one_shot_using_guid 等）。
///     这是 RitsuLib FmodStudioGateway 的同思路重写，仅参考其方法名，不引用其代码。
/// </summary>
internal static class FmodInterop
{
    /// <summary>FMOD GDExtension 暴露的 Godot 单例名。</summary>
    private static readonly StringName ServerName = new("FmodServer");

    // ---- FmodServer 上的 GDExtension 方法名（与游戏内 FMOD addon 一致） ----
    /// <summary>加载一个 bank 文件：load_bank(res_path, mode)。</summary>
    internal static readonly StringName LoadBank = new("load_bank");

    /// <summary>阻塞直到所有非阻塞 bank 加载完成。</summary>
    internal static readonly StringName WaitForAllLoads = new("wait_for_all_loads");

    /// <summary>校验某事件 GUID 是否存在于已加载的 Studio 数据中：check_event_guid(braced_guid)。</summary>
    internal static readonly StringName CheckEventGuid = new("check_event_guid");

    /// <summary>校验某事件路径是否存在：check_event_path(path)。mod bank 无 strings.bank 时一般返回 false。</summary>
    internal static readonly StringName CheckEventPath = new("check_event_path");

    /// <summary>按 GUID 触发一次性音效：play_one_shot_using_guid(braced_guid)。</summary>
    internal static readonly StringName PlayOneShotUsingGuid = new("play_one_shot_using_guid");

    /// <summary>按 GUID 触发带参一次性音效：play_one_shot_using_guid_with_params(braced_guid, params_dict)。</summary>
    internal static readonly StringName PlayOneShotUsingGuidWithParams = new("play_one_shot_using_guid_with_params");

    /// <summary>按 GUID 创建一个可长期持有的事件实例（用于 BGM / loop）：create_event_instance_with_guid(braced_guid)。</summary>
    internal static readonly StringName CreateEventInstanceWithGuid = new("create_event_instance_with_guid");

    /// <summary>统计已加载 bank 数量（诊断用）。</summary>
    internal static readonly StringName GetAllBanks = new("get_all_banks");

    /// <summary>按 GUID 解析事件描述（诊断用）：get_event_from_guid(braced_guid)。</summary>
    internal static readonly StringName GetEventFromGuid = new("get_event_from_guid");

    // ---- 裸文件加载相关（不走 bank，直接读 wav/ogg/mp3）----
    /// <summary>把裸音频文件加载为 sound（短音效）：load_file_as_sound(res_or_abs_path)。</summary>
    internal static readonly StringName LoadFileAsSound = new("load_file_as_sound");

    /// <summary>把裸音频文件加载为流式 music（BGM）：load_file_as_music(res_or_abs_path)。</summary>
    internal static readonly StringName LoadFileAsMusic = new("load_file_as_music");

    /// <summary>为已加载的裸文件创建可播放实例：create_sound_instance(path)。返回实例对象，可 play/stop/set_volume。</summary>
    internal static readonly StringName CreateSoundInstance = new("create_sound_instance");

    /// <summary>卸载裸文件：unload_file(path)。</summary>
    internal static readonly StringName UnloadFile = new("unload_file");

    /// <summary>
    ///     返回 FmodServer 单例；不存在或获取失败时返回 null（绝不抛出）。
    /// </summary>
    public static GodotObject? TryGetServer()
    {
        try
        {
            return !Engine.HasSingleton(ServerName) ? null : Engine.GetSingleton(ServerName);
        }
        catch (Exception ex)
        {
            // 拿单例本身失败属异常情况，记录但不让其向上冒泡破坏游戏流程
            GD.PushWarning($"[WildHunt][Audio] FmodServer 单例获取失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     受保护地调用 FmodServer 的某个方法并返回结果；失败时返回 false 且 result 为默认值。
    /// </summary>
    public static bool TryCall(out Variant result, StringName method, params Variant[] args)
    {
        result = default;
        var server = TryGetServer();
        if (server is null)
            return false;

        try
        {
            // 无参与有参分开调用，避免空数组触发歧义
            result = args.Length == 0 ? server.Call(method) : server.Call(method, args);
            return true;
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[WildHunt][Audio] FMOD {method} 调用失败: {ex.Message}");
            return false;
        }
    }

    /// <summary>不关心返回值的便捷重载。</summary>
    public static bool TryCall(StringName method, params Variant[] args)
    {
        return TryCall(out _, method, args);
    }

    /// <summary>
    ///     把任意 GUID 字符串规范化为 FMOD GDExtension 要求的带花括号小写格式（如 "{xxxxxxxx-....}"）。
    ///     依据：fmod-gdextension 用 sscanf("{%8x-...}") 解析 GUID，字符串必须含花括号。
    /// </summary>
    public static bool TryNormalizeGuid(string raw, out string bracedLowercase)
    {
        bracedLowercase = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var trimmed = raw.Trim();
        // 已带花括号的先剥掉，统一交给 Guid.Parse
        if (trimmed.Length >= 2 && trimmed[0] == '{' && trimmed[^1] == '}')
            trimmed = trimmed[1..^1].Trim();

        if (!Guid.TryParse(trimmed, out var guid))
            return false;

        bracedLowercase = guid.ToString("B"); // "B" = 带花括号格式
        return true;
    }
}
