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
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace HeathcliffWildHuntMod.Powers;

/// <summary>
/// 靠近的勇气（觉悟卡 <see cref="HeathcliffWildHuntMod.Cards.Resolve"/> 施加的承载被动）。效果：
/// <list type="number">
///   <item>回合结束时：自身恢复 10 点理智。</item>
///   <item>每回合开始：自身获得 3 层<b>临时</b>力量(<see cref="StrengthPower"/>) + 5 层 <see cref="GuardPower"/>(守护)。</item>
///   <item>受到攻击时：恢复 5 点理智；攒「下回合 +1 力量」(每回合最多 3 次)，于下个回合开始兑现为<b>临时</b>力量。</item>
///   <item>回合开始时：若自身理智 ≤ 0，则本回合额外获得 3 层<b>临时</b>力量。</item>
///   <item>回合开始判定：若除自身外存活友方单位 ≥ 1，则本回合自身体力不会被扣到 1 以下(见 <see cref="ModifyHpLostAfterOstyLate"/>)。</item>
/// </list>
/// <para>
/// <b>所有力量均为临时</b>：本回合开始发放的全部力量（固定 3 + 兑现的下回合力量 + 理智≤0 的额外 3）
/// 累计到 <see cref="_strengthGiftedThisTurn"/>，回合末一次性全额回收，仅当回合生效。
/// 实现模式仿 <see cref="CoffinPower"/>（发 StrengthPower + 字段记账 + 回合末回收）。
/// </para>
/// </summary>
public sealed class CourageNearbyPower : PowerModel
{
    /// <summary>本回合受击攒的「下回合 +1 力量」次数（上限 3），下个回合开始兑现为临时力量。</summary>
    private int _pendingNextTurnStrength;
    /// <summary>本回合已通过受击触发的恢复次数（每回合最多 3 次）。</summary>
    private int _takeHitsThisTurn;
    /// <summary>本回合发放的全部临时力量总量，回合末一次性回收。</summary>
    private int _strengthGiftedThisTurn;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.None;

    /// <summary>每回合开始固定获得的力量层数。</summary>
    private const int StrengthPerTurn = 3;
    /// <summary>每回合开始固定获得的守护层数。</summary>
    private const int GuardPerTurn = 5;
    /// <summary>回合末恢复的理智量。</summary>
    private const int SanityRestorePerTurnEnd = 10;
    /// <summary>受击恢复的理智量。</summary>
    private const int SanityRestoreOnHit = 5;
    /// <summary>受击触发的每回合次数上限。</summary>
    private const int MaxHitsPerTurn = 3;
    /// <summary>理智≤0 时本回合额外力量层数。</summary>
    private const int LowSanityBonusStrength = 3;

    /// <summary>回合结束时：恢复 10 理智 + 回收本回合发放的全部临时力量。</summary>
    // 两版 dll 实测都用 BeforeTurnEnd(ctx, side)（无 participants）；反编译树里的 BeforeSideTurnEnd 与 dll 不符，统一用 BeforeTurnEnd。
    public override async Task BeforeTurnEnd(PlayerChoiceContext ctx, CombatSide side)
    {
        var participants = base.Owner.CombatState?.GetCreaturesOnSide(side) ?? (IReadOnlyList<Creature>)System.Array.Empty<Creature>();
        if (!participants.Contains(base.Owner)) return;
        await SanityPower.Restore(ctx, base.Owner, SanityRestorePerTurnEnd, base.Owner, null);
        // 回收本回合发放的全部临时力量，使其仅当回合生效
        if (_strengthGiftedThisTurn > 0)
        {
            await PowerCmd.Apply<StrengthPower>(ctx, base.Owner, -_strengthGiftedThisTurn, base.Owner, null);
            _strengthGiftedThisTurn = 0;
        }
    }

    /// <summary>受到攻击(真扣血)时：恢复 5 理智 + 攒「下回合 +1 力量」，每回合最多 3 次。</summary>
    public override async Task AfterDamageReceived(PlayerChoiceContext ctx, Creature target, DamageResult result,
        ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != base.Owner) return;
        if (!props.HasFlag(ValueProp.Move)) return;
        if (result.UnblockedDamage <= 0) return;       // 只在真受到攻击伤害时触发
        if (_takeHitsThisTurn >= MaxHitsPerTurn) return;
        _takeHitsThisTurn++;
        await SanityPower.Restore(ctx, base.Owner, SanityRestoreOnHit, base.Owner, null);
        _pendingNextTurnStrength++;                    // 下回合开始兑现
    }

    /// <summary>
    /// 回合开始：发 3 临时力量 + 5 守护；兑现上回合攒的「下回合 +1 力量」(临时)；理智≤0 时额外发 3 临时力量。
    /// 所有力量都累计到 <see cref="_strengthGiftedThisTurn"/>，回合末统一回收。
    /// </summary>
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext ctx, Player player)
    {
        if (player != base.Owner.Player) return;

        // 本回合应发放的临时力量总量
        int strengthToGive = StrengthPerTurn;                 // ① 固定每回合 +3
        if (_pendingNextTurnStrength > 0)                     // ② 兑现上回合受击攒下的「下回合 +N 力量」
        {
            strengthToGive += _pendingNextTurnStrength;
            _pendingNextTurnStrength = 0;
        }
        var sanity = base.Owner.GetPower<SanityPower>()?.Amount ?? 0;
        if (sanity <= 0)                                      // ③ 理智≤0 → 额外 +3
            strengthToGive += LowSanityBonusStrength;

        // 一次性发放并记账（回合末由 BeforeSideTurnEnd 全额回收 → 全部为临时力量）
        if (strengthToGive > 0)
        {
            await PowerCmd.Apply<StrengthPower>(ctx, base.Owner, strengthToGive, base.Owner, null);
            _strengthGiftedThisTurn += strengthToGive;
        }

        // 守护：5 层（GuardPower 回合末自身清零）
        await PowerCmd.Apply<GuardPower>(ctx, base.Owner, GuardPerTurn, base.Owner, null);

        // 重置本回合受击计数
        _takeHitsThisTurn = 0;
    }

    /// <summary>
    /// 保命：若除自身外存活友方(随从)≥1，则本回合扣血最多让自身体力剩 1，不会被打死。
    /// 走 AfterOstyLate（最末期）确保盖过其它减伤后仍生效。
    /// </summary>
    public override decimal ModifyHpLostAfterOstyLate(
        Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != base.Owner) return amount;
        if (amount <= 0m) return amount;

        // 除自身外存活友方单位数（随从挂在 PlayerCombatState.Pets）
        int allies = base.Owner.Player?.PlayerCombatState.Pets.Count(p => p != base.Owner && p.IsAlive) ?? 0;
        if (allies < 1) return amount;

        // 把伤害削到「最多让 HP 剩 1」：当前血 - 伤害 < 1 时，只扣到剩 1
        decimal maxLoss = base.Owner.CurrentHp - 1;
        if (maxLoss < 0m) maxLoss = 0m;
        return amount > maxLoss ? maxLoss : amount;
    }
}

