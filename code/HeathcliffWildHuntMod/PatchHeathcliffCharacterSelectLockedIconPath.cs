using HarmonyLib;
using MegaCrit.Sts2.Core.Models;

namespace HeathcliffWildHuntMod;

/// <summary>
/// 给 <see cref="Heathcliff"/> 的"锁定状态图标路径"也指到一张存在的 PNG，避免选人界面在某些边缘流程
/// （如 <c>UnlockIfPossible</c>、<c>LockForAnimation</c>、AssetPaths 预加载）触到不存在路径而崩。
/// <para>
/// 当前 <see cref="Heathcliff.UnlocksAfterRunAs"/> 返回 null（开发期始终解锁），
/// 正常流程下按钮不会进 <c>_isLocked = true</c> 分支，因此这张图基本不会展示给玩家；
/// 只是用作 <c>AssetPaths</c> 列表里被引用时的占位，防止 PreloadManager 报错。
/// </para>
/// <para>
/// 简化处理：复用解锁状态的 <c>10710_normal_info.png</c>，避免再做一张灰度图；后续若做了 locked 专属图再切换路径。
/// </para>
/// </summary>
[HarmonyPatch(typeof(CharacterModel), "get_CharacterSelectLockedIconPath")]
internal static class PatchHeathcliffCharacterSelectLockedIconPath
{
    // 暂时与解锁图共用一张 PNG；正式发布前若需要灰度/锁链效果，把路径换成专属 png 即可
    private const string HeathcliffLockedIconPath =
        "res://animations/character_select/Heathcliff-WildHunt/10710_normal_info.png";

    private static void Postfix(CharacterModel __instance, ref string __result)
    {
        if (__instance is not Heathcliff) return;

        __result = HeathcliffLockedIconPath;
    }
}
