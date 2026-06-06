using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Saves.Managers;

namespace HeathcliffWildHuntMod;

[HarmonyPatch(typeof(ProgressSaveManager), "CheckFifteenElitesDefeatedEpoch")]
internal static class PatchCheckFifteenElites
{
    private static bool Prefix(Player localPlayer)
        => localPlayer.Character is not Heathcliff;
}

[HarmonyPatch(typeof(ProgressSaveManager), "CheckFifteenBossesDefeatedEpoch")]
internal static class PatchCheckFifteenBosses
{
    private static bool Prefix(Player localPlayer)
        => localPlayer.Character is not Heathcliff;
}

[HarmonyPatch(typeof(ProgressSaveManager), "ObtainCharUnlockEpoch")]
internal static class PatchObtainCharUnlock
{
    private static bool Prefix(Player localPlayer)
        => localPlayer.Character is not Heathcliff;
}
