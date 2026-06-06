using HarmonyLib;
using HeathcliffWildHuntMod.Monsters;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace HeathcliffWildHuntMod;

/// <summary>
/// vanilla NCreature.ToggleIsInteractable(false) 会把 _stateDisplay.Visible 一同关掉，
/// 导致玩家宠物默认无血条。狂猎随从需要可见的血条，重新打开。
/// </summary>
[HarmonyPatch(typeof(NCreature), nameof(NCreature.ToggleIsInteractable))]
internal static class PatchMinionShowHealthBar
{
    private static void Postfix(NCreature __instance, bool on)
    {
        if (on) return;
        if (__instance.Entity?.Monster is not WildHuntMinionModel) return;

        var stateDisplay = Traverse.Create(__instance).Field("_stateDisplay").GetValue<NCreatureStateDisplay>();
        if (stateDisplay != null && !NCombatUi.IsDebugHidingHpBar)
            stateDisplay.Visible = true;
    }
}
