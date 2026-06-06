using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using HeathcliffWildHuntMod.EgoPile;

namespace HeathcliffWildHuntMod;

/// <summary>
/// 把 <see cref="CardPile.Get"/> 对 EGO 自定义 <see cref="PileType"/> 的查询短路到 <see cref="EgoPileStorage"/>。
/// <para>
/// vanilla <c>CardPile.Get</c> 是个 switch，遇到非 0–6 的自定义 PileType 会落到 <c>default => throw</c>。
/// 因此必须用 <b>Prefix</b>（不是 Postfix）抢在 throw 之前返回我们的堆；非 EGO 类型一律放行 vanilla。
/// </para>
/// </summary>
[HarmonyPatch(typeof(CardPile), nameof(CardPile.Get))]
internal static class PatchEgoPileGet
{
    private static bool Prefix(PileType type, Player player, ref CardPile? __result)
    {
        if (type != EgoFramework.EgoPileType)
            return true; // 非 EGO 堆：放行 vanilla switch

        __result = EgoPileStorage.Resolve(player);
        return false; // EGO 堆：短路，已写入 __result
    }
}
