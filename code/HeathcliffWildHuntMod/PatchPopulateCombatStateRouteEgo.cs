using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using HeathcliffWildHuntMod.Cards;
using HeathcliffWildHuntMod.EgoPile;

namespace HeathcliffWildHuntMod;

/// <summary>
/// 建堆分流：把 EGO 卡从抽牌堆分流到 EGO 专属堆。
/// <para>
/// vanilla <see cref="Player.PopulateCombatState"/> 遍历 <c>Deck.Cards</c>，每张 clone 后 <c>AddInternal</c>
/// 进 DrawPile，最后 RandomizeOrder。EGO 卡常驻 Deck（为了随存档持久化），但不应进抽牌循环。
/// </para>
/// <para>
/// 实现：Postfix 在建堆后扫 DrawPile，把其中的 EGO 卡 <c>RemoveInternal</c> 出来、<c>AddInternal</c> 进 EGO 专属堆。
/// 选 Postfix 而非 Transpiler/Prefix——后者要拦 vanilla 内部 AddInternal 调用，脆且易与其它 patch 冲突；
/// 建堆后再搬运简单稳妥，且 RandomizeOrder 已发生、移除 EGO 不影响其余卡顺序。
/// </para>
/// </summary>
[HarmonyPatch(typeof(Player), nameof(Player.PopulateCombatState))]
internal static class PatchPopulateCombatStateRouteEgo
{
    private static void Postfix(Player __instance)
    {
        var combatState = __instance.PlayerCombatState;
        if (combatState is null)
            return;

        var drawPile = combatState.DrawPile;
        // 先收集，避免遍历时修改集合
        var egoCards = drawPile.Cards.Where(EgoCardBase.IsEgo).ToList();
        if (egoCards.Count == 0)
            return;

        var egoPile = EgoPileStorage.GetForCombatState(combatState);
        foreach (var ego in egoCards)
        {
            drawPile.RemoveInternal(ego, silent: true);
            egoPile.AddInternal(ego, silent: true);
        }
    }
}
