using System.Collections.Generic;
using System.Threading.Tasks;
using HeathcliffWildHuntMod.Powers;
using HeathcliffWildHuntMod.Visuals;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HeathcliffWildHuntMod.Cards;

/// <summary>C4 球棍痛殴：1费攻击6(8)，每有1棺额外造成一次伤害。</summary>
public sealed class ClubStrike : CardModel
{
    private const decimal BaseDamage = 6m;
    private const decimal UpgradeDamageBy = 2m;

    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Strike };
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new DamageVar(BaseDamage, ValueProp.Move) };

    public ClubStrike() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var o = base.Owner.Creature;
        var coffins = o.GetPowerAmount<CoffinPower>();
        var dmg = DynamicVars.Damage.BaseValue;

        // 基础攻击
        await DamageCmd.Attack(dmg).FromCard(this).Targeting(play.Target!)
            .WithAttackerAnim(HeathcliffAttackAnim.PickFor(this), HeathcliffAttackAnim.PickHitDelayFor(this))
            .WithHitFx("vfx/vfx_attack_slash")
            .WithHitVfxNode(c => SlashVfx.CreateFor(c))
            .Execute(ctx);

        // 每层棺额外造成一次伤害
        for (int i = 0; i < coffins; i++)
        {
            await DamageCmd.Attack(dmg).FromCard(this).Targeting(play.Target!)
                .WithAttackerAnim(HeathcliffAttackAnim.PickFor(this), HeathcliffAttackAnim.PickHitDelayFor(this))
                .WithHitFx("vfx/vfx_attack_slash")
                .WithHitVfxNode(c => SlashVfx.CreateFor(c))
                .Execute(ctx);
        }
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(UpgradeDamageBy);
}
