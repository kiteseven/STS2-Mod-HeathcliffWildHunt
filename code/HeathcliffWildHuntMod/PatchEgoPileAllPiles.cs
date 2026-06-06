using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using HeathcliffWildHuntMod.EgoPile;

namespace HeathcliffWildHuntMod;

/// <summary>
/// 把 EGO 专属堆拼进 <see cref="PlayerCombatState.AllPiles"/>，让所有「枚举战斗牌堆」的 vanilla 代码路径
/// （战斗结束清理、广播、查找等）透明地覆盖到 EGO 堆。
/// <para>
/// 关键：<see cref="HeathcliffWildHuntMod.Powers.SanityPower"/> 的 <c>PlayerHasAnyEgoCard</c> 遍历
/// <c>Player.Piles</c>（含 AllPiles）找 EGO 牌——只有 EGO 堆在 AllPiles 里，理智触底才能判定「持有 EGO」进侵蚀。
/// </para>
/// <para>
/// 仿 RitsuLib：用 Postfix 而非 Transpiler（避免 IL 冲突），把 EGO 堆拼到 vanilla 结果之后，
/// 并反射写回 private <c>_piles</c> 字段，使后续 getter 调用直接命中合并后的数组、不必每次重新分配。
/// 只在 EGO 堆尚未出现时拼接（幂等，避免重复追加）。
/// </para>
/// </summary>
[HarmonyPatch(typeof(PlayerCombatState), "get_AllPiles")]
internal static class PatchEgoPileAllPiles
{
    private static readonly AccessTools.FieldRef<PlayerCombatState, CardPile[]?> PilesField =
        AccessTools.FieldRefAccess<PlayerCombatState, CardPile[]?>("_piles");

    private static void Postfix(PlayerCombatState __instance, ref System.Collections.Generic.IReadOnlyList<CardPile> __result)
    {
        // 只取已存在的 EGO 堆，不在这里触发创建（创建交给真正往里加牌的路径）
        var egoPile = EgoPileStorage.PeekForCombatState(__instance);
        if (egoPile is null)
            return;

        // 已经在结果里则跳过（幂等）
        if (__result.Any(p => ReferenceEquals(p, egoPile)))
            return;

        var combined = new CardPile[__result.Count + 1];
        for (int i = 0; i < __result.Count; i++)
            combined[i] = __result[i];
        combined[^1] = egoPile;

        // 写回 _piles：让后续访问直接命中合并数组
        PilesField(__instance) = combined;
        __result = combined;
    }
}
