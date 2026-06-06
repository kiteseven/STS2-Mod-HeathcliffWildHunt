using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using HeathcliffWildHuntMod.Cards;
using HeathcliffWildHuntMod.EgoPile;

namespace HeathcliffWildHuntMod;

/// <summary>
/// 回合结束时，将所有 EGO 卡从手牌/消耗堆搬回 EGO 专属堆（实现可重复主动释放）。
/// 监听点：回合末/消耗堆清理后。
/// </summary>
[HarmonyPatch(typeof(PlayerCombatState), "EndTurn")]
internal static class PatchEgoReturnAfterTurn
{
    [HarmonyPostfix]
    private static async Task Postfix(PlayerCombatState __instance)
    {
        var player = __instance.Player;
        if (player is null) return;
        if (!EgoFramework.IsOwnerPlayer(player)) return;

        var egoPile = EgoPileStorage.PeekForCombatState(__instance);
        if (egoPile is null) return;

        // 收集所有非 EGO 堆的 EGO 卡（手牌 + 消耗堆）
        var allPiles = new[] { __instance.Hand, __instance.ExhaustPile };
        var egoCardsToReturn = allPiles
            .SelectMany(pile => pile.Cards)
            .Where(card => EgoCardBase.IsEgo(card) && !ReferenceEquals(card.Pile, egoPile))
            .ToList();

        if (egoCardsToReturn.Count == 0) return;

        // 逐张搬回 EGO 堆（skipVisuals 避免动画堆积）
        foreach (var card in egoCardsToReturn)
        {
            await CardPileCmd.RemoveFromCombat(card, skipVisuals: true);
            egoPile.AddInternal(card, silent: true);
        }
    }
}
