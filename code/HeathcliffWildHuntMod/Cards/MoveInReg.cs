using System.Collections.Generic;
using System.Threading.Tasks;
using HeathcliffWildHuntMod.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace HeathcliffWildHuntMod.Cards;

/// <summary>
/// EGO「迁居申请 Move-in Reg」：2费攻击（占位伤害10），释放占位扣 15 理智。
/// <para>
/// 效果：[攻击前] 获得 1 个等离子球；目标失去 2 力量；给目标 2 易伤；获得 1~3 能量。
/// </para>
/// </summary>
public sealed class MoveInReg : EgoCardBase
{
    private const decimal BaseDamage = 10m;
    private const int StrengthDown = 2;
    private const int VulnerableStacks = 2;
    private const int MinEnergy = 1;
    private const int MaxEnergy = 3;

    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Strike };
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust, CardKeyword.Ethereal };
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new DamageVar(BaseDamage, ValueProp.Move) };

    public MoveInReg() : base(2, CardType.Attack, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var o = base.Owner.Creature;
        await SanityPower.Drain(ctx, o, 15, o, this);

        // [攻击前] 获得 1 个等离子球（复用 vanilla PlasmaOrb，回合开始/被唤起给能量）
        await OrbCmd.Channel<PlasmaOrb>(ctx, base.Owner);
        // 获得 1~3 能量（含上下限的随机量）
        int energy = base.Owner.RunState.Rng.Niche.NextInt(MinEnergy, MaxEnergy + 1);
        await PlayerCmd.GainEnergy(energy, base.Owner);

        var target = play.Target!;
        // 目标 -2 力量 + 2 易伤（先于攻击，"攻击前"语义）
        await PowerCmd.Apply<StrengthPower>(ctx, target, -StrengthDown, o, this);
        await PowerCmd.Apply<VulnerablePower>(ctx, target, VulnerableStacks, o, this);

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this).Targeting(target)
            .WithAttackerAnim(HeathcliffAttackAnim.PickFor(this), HeathcliffAttackAnim.PickHitDelayFor(this))
            .WithHitFx("vfx/vfx_attack_slash").Execute(ctx);
    }
}
