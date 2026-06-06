using System.Collections.Generic;
using System.Threading.Tasks;
using HeathcliffWildHuntMod.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace HeathcliffWildHuntMod.Cards;

/// <summary>
/// EGO「AEDD」：2费攻击（占位伤害14），释放占位扣 10 理智。
/// <para>
/// 效果：[攻击前] 自身每损失 10% 体力获得 1 能量（最多 7）；获得 2 敏捷；给目标 3 层虚弱。
/// </para>
/// <para>
/// 注：与普通卡 <see cref="Aedd"/> 同名概念但定位不同（这是 EGO 版）。卡图复用 Aedd.png，
/// 由 <c>PatchCardPortraitPath</c> 把类名 AeddEgo 特例映射到 Aedd 卡图。
/// </para>
/// </summary>
public sealed class AeddEgo : EgoCardBase
{
    private const decimal BaseDamage = 14m;
    private const int DexGain = 2;
    private const int WeakStacks = 3;
    private const int MaxEnergyGain = 7;

    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Strike };
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust, CardKeyword.Ethereal };
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new DamageVar(BaseDamage, ValueProp.Move) };

    public AeddEgo() : base(2, CardType.Attack, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var o = base.Owner.Creature;
        await SanityPower.Drain(ctx, o, 10, o, this);

        // [攻击前] 每损失 10% 体力 → +1 能量（最多 7）
        int lostPercentTenths = o.MaxHp > 0 ? (o.MaxHp - o.CurrentHp) * 10 / o.MaxHp : 0;
        int energy = System.Math.Min(MaxEnergyGain, lostPercentTenths);
        if (energy > 0)
            await PlayerCmd.GainEnergy(energy, base.Owner);
        await PowerCmd.Apply<DexterityPower>(ctx, o, DexGain, o, this);

        var target = play.Target!;
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this).Targeting(target)
            .WithAttackerAnim(HeathcliffAttackAnim.PickFor(this), HeathcliffAttackAnim.PickHitDelayFor(this))
            .WithHitFx("vfx/vfx_attack_slash").Execute(ctx);

        if (target.IsAlive)
            await PowerCmd.Apply<WeakPower>(ctx, target, WeakStacks, o, this);
    }
}
