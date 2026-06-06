using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;

namespace HeathcliffWildHuntMod;

/// <summary>
/// vanilla NMerchantCharacter.PlayAnimation 直接 new MegaSprite(GetChild(0))，
/// 子 0 不是 SpineSprite 就抛 InvalidOperationException。
/// 狂猎商店用 Sprite2D 不用 Spine——子 0 不是 SpineSprite 时安全跳过。
/// </summary>
[HarmonyPatch(typeof(NMerchantCharacter), nameof(NMerchantCharacter.PlayAnimation))]
internal static class PatchMerchantSkipNonSpine
{
    static bool Prefix(NMerchantCharacter __instance)
    {
        if (__instance.GetChildCount() == 0) return false;
        var child = __instance.GetChild(0);
        return child != null && child.GetClass() == "SpineSprite";
    }
}
