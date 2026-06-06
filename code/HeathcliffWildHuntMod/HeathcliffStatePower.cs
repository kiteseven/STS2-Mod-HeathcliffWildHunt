using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HeathcliffWildHuntMod.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HeathcliffWildHuntMod;

/// <summary>
/// 隐藏 Power：跨回合 / 跨卡牌追踪本场战斗发生过的事件，供其他卡 / Power 读取。
/// 参照 Ryoshu 的 RyoshuStatePower 模式：不可见、不计层数、随玩家进入战斗时附加。
/// 承载内容：
///   - SummonsEverSummonedThisCombat：本场已召唤过的随从总数（即使阵亡也算），用于 R15 狂猎之心 / U12 等。
///   - CoffinConsumedThisTurn / CoffinGainedThisTurn：本回合棺进出量，供 R16 沸腾怨念等被动判定。
///   - EnteredDullahanThisCombat：本场是否已进入过杜拉罕，供"首次进入"奖励类卡牌判定。
///   - PendingDullahanAtTurnEnd：B4 杜拉罕啊……！未达理智阈值时的延迟入场队列。
/// </summary>
public sealed class HeathcliffStatePower : PowerModel
{
    // 本场战斗已召唤过的随从计数（包括已阵亡的）
    private int _summonsEverSummonedThisCombat;
    // 本回合内消耗 / 获得的棺量，每回合开始清零
    private int _coffinConsumedThisTurn;
    private int _coffinGainedThisTurn;
    // 本场是否进入过杜拉罕（"本场首次进入杜拉罕"奖励触发用）
    private bool _enteredDullahanThisCombat;
    // 本回合结束时应额外获得的杜拉罕层数（B4 在不满足理智门槛时排队到回合末）
    private int _pendingDullahanAtTurnEnd;
    // 本回合结束时是否要解除全部杜拉罕（[[Lament]] 打出后置位，到 SideTurnEnd 统一执行）
    private bool _requestRemoveDullahanAtTurnEnd;
    // 本场战斗累计消耗（Exhaust）的牌数，供"消耗 archetype"卡牌（如 [[AshFrenzy]]）读取做长线 scaling
    private int _cardsExhaustedThisCombat;

    /// <summary>对玩家不可见，纯逻辑追踪 Power。</summary>
    protected override bool IsVisibleInternal => false;

    public override PowerType Type => PowerType.Buff;

    /// <summary>不参与层数显示。</summary>
    public override PowerStackType StackType => PowerStackType.None;

    /// <summary>本场召唤随从累计计数。</summary>
    public int SummonsEverSummonedThisCombat
    {
        get => _summonsEverSummonedThisCombat;
        set { AssertMutable(); _summonsEverSummonedThisCombat = value; }
    }

    /// <summary>本回合消耗的棺量。</summary>
    public int CoffinConsumedThisTurn
    {
        get => _coffinConsumedThisTurn;
        set { AssertMutable(); _coffinConsumedThisTurn = value; }
    }

    /// <summary>本回合获得的棺量。</summary>
    public int CoffinGainedThisTurn
    {
        get => _coffinGainedThisTurn;
        set { AssertMutable(); _coffinGainedThisTurn = value; }
    }

    /// <summary>本场是否进入过杜拉罕形态。</summary>
    public bool EnteredDullahanThisCombat
    {
        get => _enteredDullahanThisCombat;
        set { AssertMutable(); _enteredDullahanThisCombat = value; }
    }

    /// <summary>本回合结束时应额外获得的杜拉罕层数（B4 杜拉罕啊……！ 在不满阈值时排队）。</summary>
    public int PendingDullahanAtTurnEnd
    {
        get => _pendingDullahanAtTurnEnd;
        set { AssertMutable(); _pendingDullahanAtTurnEnd = value; }
    }

    /// <summary>
    /// 本回合结束时是否解除全部杜拉罕。<see cref="HeathcliffWildHuntMod.Cards.Lament"/> 打出后置 true，
    /// 由 <see cref="BeforeSideTurnEnd"/> 在玩家 side 回合末统一执行；执行完自动复位 false。
    /// 设计意图：本卡"使用后退出杜拉罕"如果改成立即移除，会让同回合后续卡失去杜拉罕加成；
    /// 延迟到回合末执行才符合设计案"打出 S3 后退出形态"的语义（本回合余下卡仍能享受加成）。
    /// </summary>
    public bool RequestRemoveDullahanAtTurnEnd
    {
        get => _requestRemoveDullahanAtTurnEnd;
        set { AssertMutable(); _requestRemoveDullahanAtTurnEnd = value; }
    }

    /// <summary>本场战斗累计消耗（Exhaust）的牌数。消耗向卡牌（如 [[AshFrenzy]]）读此值做 scaling。</summary>
    public int CardsExhaustedThisCombat
    {
        get => _cardsExhaustedThisCombat;
        set { AssertMutable(); _cardsExhaustedThisCombat = value; }
    }

    /// <summary>任意牌被消耗时累加本场计数（含 ethereal 自动消耗）。只统计玩家方自己的牌。</summary>
    public override Task AfterCardExhausted(PlayerChoiceContext choiceContext, CardModel card, bool causedByEthereal)
    {
        if (card.Owner == base.Owner.Player)
            CardsExhaustedThisCombat++;
        return Task.CompletedTask;
    }

    /// <summary>玩家回合开始：重置每回合统计。</summary>
    public override Task AfterPlayerTurnStartEarly(PlayerChoiceContext choiceContext, Player player)
    {
        if (player == base.Owner.Player)
        {
            // 棺进出量是"每回合"快照，回合开始清零
            CoffinConsumedThisTurn = 0;
            CoffinGainedThisTurn = 0;
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 玩家所在 side 的回合结束（早期）：把 B4 排队的杜拉罕层数实际应用上去；
    /// 同时处理 [[Lament]] 触发的"打出后回合末退杜拉罕"。
    /// 用 <see cref="BeforeSideTurnEnd"/> 而非 <c>AtTurnEnd</c>（PowerModel 无此虚方法）；
    /// 选择 BeforeSideTurnEnd 而非 BeforeSideTurnEndVeryEarly 是因为入场要在 DullahanPower 的扣理智之后，
    /// 否则刚加的层会被同回合的扣理智立刻判定为 force-exit。
    /// </summary>
    // 两版 dll 实测都用 BeforeTurnEnd(ctx, side)（无 participants）；反编译树里的 BeforeSideTurnEnd 与 dll 不符，统一用 BeforeTurnEnd。
    public override async Task BeforeTurnEnd(PlayerChoiceContext choiceContext, CombatSide side)
    {
        var participants = base.Owner.CombatState?.GetCreaturesOnSide(side) ?? (IReadOnlyList<Creature>)System.Array.Empty<Creature>();
        if (!participants.Contains(base.Owner)) return;

        // ① Lament 退杜拉罕优先于 ② B4 入杜拉罕：避免本回合先入再退的无效操作
        if (_requestRemoveDullahanAtTurnEnd)
        {
            _requestRemoveDullahanAtTurnEnd = false;
            var dullahan = base.Owner.GetPower<DullahanPower>();
            if (dullahan is not null && dullahan.Amount > 0)
            {
                await PowerCmd.ModifyAmount(choiceContext, dullahan, -dullahan.Amount, base.Owner, null);
            }
        }

        // ② B4 排队的入场
        if (_pendingDullahanAtTurnEnd > 0)
        {
            int amount = _pendingDullahanAtTurnEnd;
            // 取出排队层数后立刻清零，避免 PowerCmd.Apply 内部回调再次读到导致重复入场
            PendingDullahanAtTurnEnd = 0;
            await PowerCmd.Apply<DullahanPower>(choiceContext, base.Owner, amount, base.Owner, null);
        }
    }
}
