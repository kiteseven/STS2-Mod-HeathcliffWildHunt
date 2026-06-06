using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using HeathcliffWildHuntMod.EgoPile;
using HeathcliffWildHuntMod.Compat;

namespace HeathcliffWildHuntMod.Powers;

/// <summary>
/// 侵蚀 Erosion（玩家触底 -45 理智 + 持有 EGO 时进入的强制状态）。
/// <para>
/// 行为：
/// 1. 每个回合开始时（<see cref="AfterPlayerTurnStart"/>），从玩家牌堆里随机挑一张 EGO 牌
///    强制塞入手牌（来源：Deck → Discard → Exhaust 优先级，找到一张就停）；
/// 2. 该 EGO 牌必须本回合<b>第一个被打出</b>——通过监听 <see cref="AfterCardPlayed"/>
///    校验首张是否为 EGO；不是的话目前只打 warning（"必须第一个打"的强制约束需要 patch
///    NCard.OnPlay 之类的 vanilla 入口实现，本版先做"塞牌+提示"，硬性拦截放到下一版）；
/// 3. 离场条件：理智回到 &gt; -45 时由外部（卡牌效果）将本 power 移除；
///    战斗结束时本 power 随其他玩家 power 一同清除。
/// </para>
/// <para>
/// <b>显性、可见、有层数</b>。当前层数 1 表示处于侵蚀状态；多层暂无叠加效果，向下兼容做留口。
/// </para>
/// </summary>
public sealed class ErosionPower : PowerModel
{
    public override PowerType Type => PowerType.Debuff;

    /// <summary>层数显示（按设计案是状态而非计数，但用 Counter 让 UI 显示数字 1）。</summary>
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override bool IsVisibleInternal => true;

    /// <summary>
    /// 玩家回合开始：把一张 EGO 牌塞进手牌。
    /// 用 <see cref="AfterPlayerTurnStart"/> 而不是 <c>BeforeSideTurnStart</c>：
    /// 后者发生在抽牌之前，塞进的牌会跟其他抽牌混在一起；前者抽牌已结算，
    /// 直接 AddGeneratedCardToCombat 到 Hand 是"已经在手上的额外一张"，更符合"侵蚀强制塞牌"的语义。
    /// </summary>
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != base.Owner.Player) return;
        if (base.Amount <= 0) return;

        // 找一张 EGO 牌的实例 —— 优先从牌堆/弃牌堆/消耗堆里挑（不破坏总数），
        // 都没有再走"生成新实例"路线（理论上不会发生，因为进入侵蚀的前提就是有 EGO）
        var egoCard = FindAnyEgoCard(player);
        if (egoCard == null) return;

        // AddGeneratedCardToCombat 会把 card 实例放进 Hand 顶部并播抽牌动画;
        // 关键：用现有实例时不能让它重复出现在原 pile，应先把它从原 pile 移走再 add。
        // 这里我们直接用 PileType.Hand + Top 位置塞进去，源 pile 由 vanilla 的 AddGeneratedCardToCombat
        // 负责更新（它会在内部把 card.Owner 改成 player，但 _不会_ 自动从原 pile 移除）。
        // 因此手动先 RemoveFromCombat 再 Add 才安全。
        await CardPileCmd.RemoveFromCombat(egoCard, skipVisuals: true);
        await CompatCmd.AddGeneratedCardToCombat(egoCard, PileType.Hand, player, CardPilePosition.Top);
    }

    /// <summary>
    /// 侵蚀状态下「本回合首张必须打 EGO」的硬性拦截：在首张牌打出前，
    /// <see cref="ShouldPlay"/> 对非 EGO 牌返回 false（不可打），强制玩家先打 EGO。
    /// 首张打出后由 <see cref="AfterCardPlayed"/> 置 <see cref="_firstCardThisTurnPlayed"/>，解除当回合限制。
    /// </summary>
    private bool _firstCardThisTurnPlayed;

    public override Task AfterPlayerTurnStartEarly(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == base.Owner.Player)
        {
            // 每回合开始重置：新回合的首张再次受 EGO 限制
            _firstCardThisTurnPlayed = false;
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 硬拦截：处于侵蚀且本回合尚未打出过牌时，非 EGO 牌一律不可打（首张必须是 EGO）。
    /// <para>
    /// vanilla <c>CardModel.CanPlay</c> 调 <c>Hook.ShouldPlay</c> 遍历所有 model 的本方法，任一返回 false
    /// 即该卡不可打——故无需 patch NCard。注意本方法是「可打出性查询」，可能每帧被调多次，<b>不得改状态</b>。
    /// </para>
    /// </summary>
    public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType)
    {
        // 不是侵蚀状态 / 不是本玩家的牌 / 本回合已打过首张 → 不限制
        if (base.Amount <= 0) return true;
        if (card.Owner != base.Owner.Player) return true;
        if (_firstCardThisTurnPlayed) return true;
        // 本回合首张：只允许 EGO
        return IsEgoCard(card);
    }

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        // 标记本回合首张已打出，解除后续牌的 EGO 限制
        if (cardPlay.Card.Owner != base.Owner.Player) return Task.CompletedTask;
        if (_firstCardThisTurnPlayed) return Task.CompletedTask;
        _firstCardThisTurnPlayed = true;
        return Task.CompletedTask;
    }

    /// <summary>
    /// 从玩家牌堆里挑一张 EGO 牌。优先级：EGO 专属堆 &gt; 抽牌堆 &gt; 弃牌堆 &gt; 消耗堆（找到第一张就返回）。
    /// EGO 卡平时被 PatchPopulateCombatStateRouteEgo 分流进 EGO 专属堆，所以正常情况都从专属堆取。
    /// 不挑 Hand 里的：手里如果已经有 EGO，回合开始再塞一张是允许的（叠加压力）。
    /// </summary>
    private static CardModel? FindAnyEgoCard(Player player)
    {
        if (player?.PlayerCombatState == null) return null;
        var combatState = player.PlayerCombatState;
        // 优先 EGO 专属堆（PeekForCombatState 不触发创建，没建过就为 null）
        return TryFromPile(EgoPileStorage.PeekForCombatState(combatState)?.Cards)
            ?? TryFromPile(combatState.DrawPile.Cards)
            ?? TryFromPile(combatState.DiscardPile.Cards)
            ?? TryFromPile(combatState.ExhaustPile.Cards);
    }

    private static CardModel? TryFromPile(IEnumerable<CardModel>? cards)
    {
        if (cards == null) return null;
        return cards.FirstOrDefault(IsEgoCard);
    }

    /// <summary>判定是否为 EGO 牌：统一走 <see cref="HeathcliffWildHuntMod.Cards.EgoCardBase.IsEgo"/>。</summary>
    private static bool IsEgoCard(CardModel card)
    {
        return HeathcliffWildHuntMod.Cards.EgoCardBase.IsEgo(card);
    }
}
