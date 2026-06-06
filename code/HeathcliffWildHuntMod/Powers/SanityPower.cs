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
using MegaCrit.Sts2.Core.ValueProps;

namespace HeathcliffWildHuntMod.Powers;

/// <summary>
/// 理智 Sanity（双方共有的<b>显性</b>能力，范围 -45 ~ +45，<b>战斗全程持续存在</b>）。
/// <para>
/// 行为规范（按用户最新规则）：
/// 1. <b>始终可见</b>（IsVisibleInternal=true），不因层数 0 自动移除（由 [[PatchSanityNeverRemoved]] 拦截）；
/// 2. <b>双方都有</b>：玩家与每个敌人在战斗开始时都自动持有一份，由 [[PatchCombatStartAttachSanity]] 注入；
/// 3. <b>造成伤害打中血 → 自身理智 +5</b>（双方）；
/// 4. <b>造成伤害被对方完全格挡 → 自身理智 -5</b>（双方）；
/// 5. <b>受到伤害扣血 → 自身理智 -5</b>（双方）；
/// 6. <b>受到伤害被完全阻挡 → 自身理智 +5</b>（双方，"撑住了"）；
/// 7. <b>玩家方理智 &gt; 15</b>：触发杜拉罕额外效果（具体由 [[DullahanPower]] / 卡牌读 IsHigh 实现，本类不直接处理）；
/// 8. <b>玩家方理智 &lt; -15</b>：强退杜拉罕（由 [[DullahanPower]] 的回合末逻辑检查并执行）；
/// 9. <b>玩家方触底 -45</b>：仅在持有 EGO 卡时进入 [[ErosionPower]] 侵蚀（不强制眩晕）；
/// 10. <b>敌方理智 &lt; 0</b>：解锁卡牌额外效果（由卡牌读 IsPanic 实现，本类不直接处理）；
/// 11. <b>敌方触底 -45</b>：强制眩晕一回合并重置 0（由 HandleBottomReached 处理）。
/// 注：玩家回合末"+1 杜拉罕 / 解除杜拉罕"是 [[DullahanPower]] 的形态机制，<b>不在</b>本类。
/// </para>
/// </summary>
public sealed class SanityPower : PowerModel
{
    public const int MinSanity = -45;
    public const int MaxSanity = 45;
    public const int Threshold = -45;

    /// <summary>造成伤害打中血时本方理智增量（双方通用）。</summary>
    public const int OnDealDamageGain = 5;
    /// <summary>受到伤害扣血时本方理智减量（双方通用）。</summary>
    public const int OnTakeDamageDrain = 5;
    /// <summary>受到伤害被完全阻挡时本方理智增量（双方通用，"完全阻挡 +"）。</summary>
    public const int OnFullBlockGain = 5;
    /// <summary>造成伤害被对方完全格挡时本方理智减量（双方通用）。</summary>
    public const int OnAttackBlockedDrain = 5;

    // 同卡去重：同一张卡的多段攻击只计一次理智变化
    private CardModel? _lastDealCard;
    private CardModel? _lastTakeCard;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override bool IsVisibleInternal => true;

    /// <summary>允许负数：理智范围跨 0，且 0 也要显示，不允许 vanilla "0 即不存在" 的兜底逻辑。</summary>
    public override bool AllowNegative => true;

    /// <summary>理智变化频率极高（打中/被打/格挡都触发），每次弹 VFX 会叠加卡住。关闭 VFX。</summary>
    public override bool ShouldPlayVfx => false;

    public bool IsAtBottom => base.Amount <= MinSanity;

    /// <summary>"理智够高"判定：&gt;= 15。多张卡(OhDullahan/U7 等)读这个属性触发增益效果。</summary>
    public bool IsHigh => base.Amount >= 15;

    public bool IsPanic => base.Amount < 0;

    /// <summary>
    /// 造成 Move 伤害结算后调整自身理智：
    ///   - 真打到血（UnblockedDamage &gt; 0）→ +<see cref="OnDealDamageGain"/>
    ///   - 被对方完全格挡（UnblockedDamage == 0 但有 BlockedDamage）→ -<see cref="OnAttackBlockedDrain"/>
    /// 双方通用。
    /// </summary>
    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result,
        ValueProp props, Creature target, CardModel? cardSource)
    {
        if (dealer != base.Owner) return;
        if (!props.HasFlag(ValueProp.Move)) return;
        // 同卡多段攻击只计第一次
        if (cardSource != null && ReferenceEquals(cardSource, _lastDealCard)) return;
        _lastDealCard = cardSource;

        if (result.UnblockedDamage > 0)
            await Restore(choiceContext, base.Owner, OnDealDamageGain, dealer, cardSource);
        else if (result.BlockedDamage > 0)
            await Drain(choiceContext, base.Owner, OnAttackBlockedDrain, dealer, cardSource);
    }

    /// <summary>
    /// 受到 Move 伤害结算后调整自身理智：
    ///   - 扣了血（UnblockedDamage &gt; 0）→ -<see cref="OnTakeDamageDrain"/>
    ///   - 完全阻挡（UnblockedDamage == 0 但有 BlockedDamage）→ +<see cref="OnFullBlockGain"/>（"撑住了"）
    /// 双方通用。
    /// </summary>
    public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext, Creature target, DamageResult result,
        ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != base.Owner) return;
        if (!props.HasFlag(ValueProp.Move)) return;
        if (cardSource != null && ReferenceEquals(cardSource, _lastTakeCard)) return;
        _lastTakeCard = cardSource;

        if (result.UnblockedDamage > 0)
        {
            // 真扣血：target 理智 -
            await Drain(choiceContext, base.Owner, OnTakeDamageDrain, dealer, cardSource);
        }
        else if (result.BlockedDamage > 0)
        {
            // 完全阻挡（伤害全被格挡撑住）：target 理智 +
            await Restore(choiceContext, base.Owner, OnFullBlockGain, dealer, cardSource);
            // 派一次 "Block" trigger：Spine animator 的 AnyState 有 Block 分支 → 播 block 动画。
            base.Owner.GetCreatureNode()?.SetAnimationTrigger("Block");
        }
    }

    // 注：旧版曾在此 override BeforeSideTurnEnd 调 EgoFramework.SweepEgoCardsBackToPile，
    //     把回合末散落在战斗堆里的 EGO 卡搬回专属堆。但该 sweep 在回合切换的 RemoveInternal/AddInternal
    //     会卡住回合末流程（手上留有 EGO 卡时无法进入下一回合）。现已改为给 EGO 卡加 Ethereal(虚无) 关键字，
    //     未打出的 EGO 卡由 vanilla 回合末逻辑自动消耗(烧掉)，无需本类介入。该 override 已删除。

    /// <summary>
    /// 区分 EGO 卡两种「进消耗堆」的结局，仅<b>虚无烧掉(没打出)</b>时把卡退回 EGO 专属堆：
    ///   - <c>causedByEthereal == true</c>：玩家本回合<b>没打出</b>，被虚无烧 → <b>退回专属堆</b>，下次还能再释放（反悔不损失）；
    ///   - <c>causedByEthereal == false</c>：玩家<b>确认打出</b>后的消耗 → 不退回，按一次性消耗（设计：打出即用掉）。
    /// <para>
    /// 时序：vanilla <c>CardCmd.Exhaust</c> 先把卡 Add 进消耗堆再调本钩子，故此刻 <c>card.Pile</c> 是消耗堆，
    /// <see cref="EgoPile.EgoFramework.ReturnCardToEgoPile"/> 会把它从消耗堆摘出再塞回专属堆（silent，不触发视觉）。
    /// owner 门禁 + 仅认本程序集 EGO 卡的隔离都在 <c>ReturnCardToEgoPile</c> / <see cref="Cards.EgoCardBase.IsEgo"/> 内。
    /// </para>
    /// </summary>
    public override Task AfterCardExhausted(PlayerChoiceContext choiceContext, CardModel card, bool causedByEthereal)
    {
        // 只处理「本玩家身上的理智 power」对应玩家的卡，且只在虚无烧掉(没打出)时退回
        if (base.Owner.IsPlayer && causedByEthereal && Cards.EgoCardBase.IsEgo(card))
        {
            EgoPile.EgoFramework.ReturnCardToEgoPile(base.Owner.Player, card);
        }
        return Task.CompletedTask;
    }

    // 注：早期版本曾在这里 override ModifyHpLostAfterOsty 把"理智>0"判定为完全免伤——
    //     那是对设计案"完全阻挡 +"的误读。"完全阻挡 +"的意思是【受到伤害被完全阻挡时 +理智】，
    //     不是【理智>0 触发完全阻挡】。该逻辑已删除（之前导致玩家几回合后理智涨到 5+ 永久无敌）。
    //     现在"完全阻挡"由 vanilla 的 Block 系统自然产生（BlockedDamage > 0 且 UnblockedDamage == 0），
    //     由 AfterDamageReceived 检测后给本方加理智。

    /// <summary>
    /// 扣理智的统一入口：自动 clamp 到 [MinSanity, MaxSanity]；扣完 owner 触底时按身份分发：
    /// 敌方 → 强制眩晕一回合 + 重置 0；玩家 → 若有 EGO 进侵蚀，无 EGO 保持 -45 不动。
    /// </summary>
    public static async Task<int> Drain(
        PlayerChoiceContext ctx, Creature target, int amount,
        Creature? applier, CardModel? cardSource)
    {
        if (amount <= 0) return 0;
        var power = target.GetPower<SanityPower>();
        // 战斗开始已附加，理论上 power 一定存在；但战斗外（事件直接扣理智）兜底创建一份
        if (power is null)
        {
            await PowerCmd.Apply<SanityPower>(ctx, target, -amount, applier, cardSource);
            power = target.GetPower<SanityPower>();
            if (power is null) return amount; // 创建失败则按 amount 返回，避免死循环
        }
        else
        {
            int current = power.Amount;
            int newValue = Math.Max(MinSanity, current - amount);
            int actualDrain = current - newValue;
            if (actualDrain == 0) return 0;
            await PowerCmd.ModifyAmount(ctx, power, -actualDrain, applier, cardSource);
        }

        // 扣完后检查是否触底（用 amount 后的最新引用，不要用扣前的 cached current）
        var refreshed = target.GetPower<SanityPower>();
        if (refreshed is not null && refreshed.Amount <= Threshold)
        {
            await refreshed.HandleBottomReached(ctx, applier, cardSource);
        }

        return amount;
    }

    /// <summary>
    /// 恢复理智的统一入口：自动 clamp 到 [MinSanity, MaxSanity]，永不移除 power（即使涨到 0/正数）。
    /// </summary>
    public static async Task<int> Restore(
        PlayerChoiceContext ctx, Creature target, int amount,
        Creature? applier, CardModel? cardSource)
    {
        if (amount <= 0) return 0;
        var power = target.GetPower<SanityPower>();
        if (power is null)
        {
            await PowerCmd.Apply<SanityPower>(ctx, target, amount, applier, cardSource);
            return amount;
        }
        int current = power.Amount;
        int newValue = Math.Min(MaxSanity, current + amount);
        int actualRestore = newValue - current;
        if (actualRestore == 0) return 0;
        await PowerCmd.ModifyAmount(ctx, power, actualRestore, applier, cardSource);
        return actualRestore;
    }

    /// <summary>
    /// 触底分支：敌方一律强制眩晕 + 理智清零；玩家方仅在持有 EGO 卡时进入侵蚀，否则保持 -45 等待消化。
    /// </summary>
    private async Task HandleBottomReached(PlayerChoiceContext ctx, Creature? applier, CardModel? cardSource)
    {
        var owner = base.Owner;
        if (owner.IsEnemy)
        {
            // 敌方：调 vanilla CreatureCmd.Stun 替换下一次 Move 为 STUNNED，再把理智重置为 0（避免下回合再次触发）
            await CreatureCmd.Stun(owner);
            await PowerCmd.ModifyAmount(ctx, this, -base.Amount, applier, cardSource);
            return;
        }

        // 玩家方：只有持有 EGO 牌才进侵蚀；否则保持 -45 让卡牌效果消化（设计意图：侵蚀是 EGO 角色专属惩罚）
        if (owner.IsPlayer && PlayerHasAnyEgoCard(owner.Player))
        {
            await PowerCmd.Apply<ErosionPower>(ctx, owner, 1, owner, cardSource);
            // 不重置理智——侵蚀本身在回合开始时塞 EGO 牌，让玩家通过出 EGO 推动战斗
        }
        // 没 EGO 也没特殊处理：保持 -45 直到理智回升或战斗结束
    }

    /// <summary>
    /// 玩家是否持有任意 EGO 牌（遍历 <see cref="Player.Piles"/>：覆盖 Hand/Draw/Discard/Exhaust + Deck，
    /// 含由 <c>PatchEgoPileAllPiles</c> 拼进来的 EGO 专属堆）。
    /// 判定逐张走 <see cref="IsEgoCard"/> → <see cref="HeathcliffWildHuntMod.Cards.EgoCardBase.IsEgo"/>，
    /// 因此只认<b>本程序集</b>的 EGO 卡（天然与共存的其它 EGO mod 隔离）。
    /// </summary>
    private static bool PlayerHasAnyEgoCard(Player player)
    {
        if (player == null) return false;
        // Player.Piles 是 IEnumerable<CardPile>(战斗中含 Hand/Draw/Discard/Exhaust + Deck)。
        // 任意 pile 任意一张是 EGO 都返回 true；遇到第一张就短路退出。
        foreach (var pile in player.Piles)
        {
            if (pile == null) continue;
            // CardPile 没实现 IEnumerable;它的卡列表通过 .Cards 暴露(IReadOnlyList<CardModel>)
            foreach (var card in pile.Cards)
            {
                if (IsEgoCard(card)) return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 判定是否为 EGO 牌：统一走 <see cref="HeathcliffWildHuntMod.Cards.EgoCardBase.IsEgo"/>。
    /// </summary>
    private static bool IsEgoCard(CardModel card)
    {
        return HeathcliffWildHuntMod.Cards.EgoCardBase.IsEgo(card);
    }
}
