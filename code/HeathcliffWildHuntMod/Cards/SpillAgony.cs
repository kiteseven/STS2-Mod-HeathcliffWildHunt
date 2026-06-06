using System.Collections.Generic;
using System.Threading.Tasks;
using HeathcliffWildHuntMod.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HeathcliffWildHuntMod.Cards;

/// <summary>C8 吐露哀嚎：1费攻击5(8)，减目标7(10)理智。</summary>
public sealed class SpillAgony : CardModel
{
    private const decimal BaseDamage = 5m;
    private const decimal UpgradeDamageBy = 3m;
    private const int DrainBase = 7;
    private const int DrainUp = 3;
    private int _drain = DrainBase;

    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Strike };
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new DamageVar(BaseDamage, ValueProp.Move) };

    public SpillAgony() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var t = play.Target!;
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this).Targeting(t)
            .WithAttackerAnim(HeathcliffAttackAnim.PickFor(this), HeathcliffAttackAnim.PickHitDelayFor(this))
            .WithHitFx("vfx/vfx_attack_slash").Execute(ctx);
        await SanityPower.Drain(ctx, t, _drain, base.Owner.Creature, this);
    }

    protected override void OnUpgrade() { DynamicVars.Damage.UpgradeValueBy(UpgradeDamageBy); _drain += DrainUp; }
}
