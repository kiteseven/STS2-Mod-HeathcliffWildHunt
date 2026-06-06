using HarmonyLib;
using MegaCrit.Sts2.Core.Models;

namespace HeathcliffWildHuntMod;

/// <summary>
/// 把 <see cref="Heathcliff"/> 的过场材质路径重定向到铁甲战士现成的
/// <c>ironclad_transition_mat.tres</c>，避免 embark 时 <c>NTransition.FadeOut</c>
/// 加载 <c>res://materials/transitions/heathcliff_transition_mat.tres</c> 失败导致黑屏。
/// <para>
/// 来源：godot.log 行 600 附近 <c>AssetLoadException: heathcliff_transition_mat.tres</c>
/// 抛在 <c>NCharacterSelectScreen.StartNewSingleplayerRun</c> 路径上，整个过场卡死。
/// </para>
/// <para>
/// 简化处理：复用铁甲战士现有的 mat.tres；后续若做了 Heathcliff 专属过场材质，
/// 把路径换成 mod pck 内的真实资源即可（参考良秀 <c>materials/transitions/ryoshu_transition_mat.tres</c>）。
/// </para>
/// </summary>
[HarmonyPatch(typeof(CharacterModel), "get_CharacterSelectTransitionPath")]
internal static class PatchHeathcliffCharacterSelectTransitionPath
{
    // 复用 vanilla 铁甲战士过场材质，避免再做一份资源
    private const string IroncladTransitionMatPath =
        "res://materials/transitions/ironclad_transition_mat.tres";

    private static void Postfix(CharacterModel __instance, ref string __result)
    {
        if (__instance is not Heathcliff) return;

        __result = IroncladTransitionMatPath;
    }
}
