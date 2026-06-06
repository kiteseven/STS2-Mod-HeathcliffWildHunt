using HarmonyLib;
using MegaCrit.Sts2.Core.Models;

namespace HeathcliffWildHuntMod;

/// <summary>仅关原版 Power VFX——能力卡（棺/杜拉罕/褪色约定等）保留特效。</summary>
[HarmonyPatch(typeof(PowerModel), "get_ShouldPlayVfx")]
internal static class PatchDisableAllPowerVfx
{
    private static void Postfix(PowerModel __instance, ref bool __result)
    {
        if (!__result) return;
        if (__instance.Owner?.Player?.Character is not Heathcliff) return;
        // 本 mod 的 Power 不放行，只关原版（Strength/Dexterity/Vulnerable 等）
        var ns = __instance.GetType().Namespace ?? "";
        // CoffinPower 变化频繁也不播 VFX
        if (__instance is Powers.CoffinPower) { __result = false; return; }
        if (ns.StartsWith("HeathcliffWildHuntMod"))
            return;
        __result = false;
    }
}
