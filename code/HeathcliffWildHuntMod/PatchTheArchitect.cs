using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Runs;

namespace HeathcliffWildHuntMod;

/// <summary>
/// TheArchitect 没有狂猎的对话条目→Dialogue=null→WinRun NRE。
/// 参照良秀的 RyoshuArchitectWinRunPatch：Dialogue 为空时跳过动画，直接 Ready。
/// </summary>
[HarmonyPatch(typeof(TheArchitect), "WinRun")]
internal static class PatchTheArchitect
{
    private static bool Prefix(TheArchitect __instance, ref Task __result)
    {
        if (AccessTools.Field(typeof(TheArchitect), "_dialogue")?.GetValue(__instance) != null)
            return true; // 有对话，正常走

        // 无对话——跳过动画，直接设 Ready
        if (LocalContext.IsMe(__instance.Owner))
            RunManager.Instance.ActChangeSynchronizer.SetLocalPlayerReady();

        __result = Task.CompletedTask;
        return false;
    }
}
