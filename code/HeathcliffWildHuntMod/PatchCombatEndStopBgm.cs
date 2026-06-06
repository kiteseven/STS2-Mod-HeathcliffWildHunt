using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;

namespace HeathcliffWildHuntMod;

/// <summary>
///     战斗结束时停掉狂猎专属 BGM 并恢复原版战斗音乐。
///     与 <see cref="PatchCombatStartAttachSanity" />（战斗开始复位触发标志）配对，
///     共同保证"每场战斗棺层首次达最大层切歌、战斗结束恢复"的语义。
///     patch 入口：<see cref="CombatManager.EndCombatInternal" /> Postfix —— 此时战斗已结算完毕，安全恢复音乐。
/// </summary>
[HarmonyPatch(typeof(CombatManager), nameof(CombatManager.EndCombatInternal))]
internal static class PatchCombatEndStopBgm
{
    /// <summary>Postfix：停 mod BGM、恢复原版音乐；并清理棺紫镜特效节点。</summary>
    private static void Postfix()
    {
        HeathcliffWildHuntMod.Audio.WildHuntBgmController.OnCombatEnd();
        HeathcliffWildHuntMod.Visuals.CoffinAura.ClearAll(); // 清理身后紫镜特效，避免跨战斗残留
    }
}
