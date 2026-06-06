using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HeathcliffWildHuntMod.Powers;

/// <summary>
/// 守护 Guard（狂猎自实现 buff）。每层使自身受到的<b>攻击伤害</b>降低 10%（乘法减伤，10 层 = 完全免疫）。
/// <para>
/// 回合末清零（类似格挡）：每回合靠「靠近的勇气」等来源重新铺层，避免无限叠加导致永久无敌。
/// 与 vanilla <see cref="MegaCrit.Sts2.Core.Models.Powers.GuardedPower"/> 不同——那是固定减半且不可叠层、
/// 主要用于联机护卫；这里做成可叠层、按层数线性减伤、回合末清空的自有 buff。
/// </para>
/// </summary>
public sealed class GuardPower : PowerModel
{
    /// <summary>每层减伤比例。</summary>
    private const decimal ReductionPerStack = 0.1m;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 攻击伤害乘法减免：目标是自身、且为「powered 攻击」时，按层数线性降低（每层 -10%，下限 0）。
    /// 非攻击伤害（如忧郁/沉沦的直接扣血）不受守护影响。
    /// </summary>
    public override decimal ModifyDamageMultiplicative(
        Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != base.Owner) return 1m;
        if (!props.IsPoweredAttack()) return 1m;
        decimal mult = 1m - ReductionPerStack * base.Amount;
        return mult < 0m ? 0m : mult;
    }

    /// <summary>回合末清零：守护不跨回合保留（每回合重新铺层）。仅在自己所在阵营回合结束、且自己是参与者时清。</summary>
    // 两版 dll 实测都用 BeforeTurnEndVeryEarly(ctx, side)（无 participants）；反编译树里的 BeforeSideTurnEndVeryEarly 与 dll 不符，统一用 BeforeTurnEndVeryEarly。
    public override async Task BeforeTurnEndVeryEarly(
        PlayerChoiceContext choiceContext, CombatSide side)
    {
        var participants = base.Owner.CombatState?.GetCreaturesOnSide(side) ?? (IReadOnlyList<Creature>)System.Array.Empty<Creature>();
        if (base.Amount <= 0) return;
        if (!participants.Contains(base.Owner)) return;
        await PowerCmd.ModifyAmount(choiceContext, this, -base.Amount, base.Owner, null);
    }
}
