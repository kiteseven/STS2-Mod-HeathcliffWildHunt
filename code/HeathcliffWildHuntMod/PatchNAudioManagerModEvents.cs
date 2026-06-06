using System;
using System.Collections.Generic;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Audio;
using HeathcliffWildHuntMod.Audio;

namespace HeathcliffWildHuntMod;

/// <summary>
///     拦截 <see cref="NAudioManager.PlayOneShot(string, System.Collections.Generic.Dictionary{string, float}, float)" />，
///     处理原版为希斯克利夫自动拼出的无效音效路径（event:/sfx/characters/heathcliff/heathcliff_*）：
///     - heathcliff_die → 重定向到 mod 的 dead 音效文件（原版死亡走这条）；
///     - 其余（attack/cast 等）已由 <see cref="PatchCreatureTriggerAnimSfx" /> 播过，这里仅抑制，避免日志刷 "cannot find sfx path"。
///     其余路径一律放行原版。
///
///     注意：音频改为裸文件加载（见 <see cref="WildHuntAudioService" />），不再走 bank/GUID，
///     故这里只负责"拦原版无效路径 + 补死亡音"，不再处理 mod 事件路径。
/// </summary>
[HarmonyPatch(typeof(NAudioManager), nameof(NAudioManager.PlayOneShot),
    new[] { typeof(string), typeof(Dictionary<string, float>), typeof(float) })]
internal static class PatchNAudioManagerModEvents
{
    /// <summary>原版按非虚 DeathSfx 为本角色自动拼出的死亡音效路径（无对应资源，需重定向）。</summary>
    private const string VanillaHeathcliffDie = "event:/sfx/characters/heathcliff/heathcliff_die";

    /// <summary>选人界面音效路径（由 Heathcliff.CharacterSelectSfx 返回，作为触发标记）。</summary>
    private const string VanillaHeathcliffSelect = "event:/sfx/characters/heathcliff/heathcliff_select";

    /// <summary>原版为本角色自动拼路径的统一前缀（attack/cast/die/select 等都在此下）。</summary>
    private const string VanillaHeathcliffPrefix = "event:/sfx/characters/heathcliff/";

    /// <summary>
    ///     Harmony 前缀：返回 false 跳过原版（已自行处理/抑制），返回 true 放行原版（非本 mod 相关路径）。
    /// </summary>
    private static bool Prefix(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return true;

        // 原版为希斯克利夫自动拼的无效路径
        if (path.StartsWith(VanillaHeathcliffPrefix, StringComparison.Ordinal))
        {
            // 死亡音效：原版在 NCreature 死亡时播 DeathSfx，这里重定向到 mod 的 dead 音效文件
            if (string.Equals(path, VanillaHeathcliffDie, StringComparison.Ordinal))
                WildHuntAudioService.PlayOneShot(WildHuntSfx.Dead);
            // 选人台词：重定向到 mod 的 select 音效文件
            else if (string.Equals(path, VanillaHeathcliffSelect, StringComparison.Ordinal))
                WildHuntAudioService.PlayOneShot(WildHuntSfx.Select);

            // 其余（heathcliff_attack / heathcliff_cast 等）已由 TriggerAnim 前缀播过，
            // 这里仅抑制原版无效路径，避免 "cannot find sfx path" 刷屏
            return false;
        }

        return true; // 与本 mod 无关：放行原版
    }
}
