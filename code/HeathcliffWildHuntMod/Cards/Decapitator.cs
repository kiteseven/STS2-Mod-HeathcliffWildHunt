using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HeathcliffWildHuntMod.Cards;

/// <summary>U10 斩首之人：0费攻击8(11)，目标非满血则额外8。</summary>
public sealed class Decapitator : CardModel
{
    private const decimal BaseDamage = 8m;
    private const decimal BonusDamage = 8m;
    private const decimal UpgradeDamageBy = 3m;

    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Strike };
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new DamageVar(BaseDamage, ValueProp.Move) };

    public Decapitator() : base(0, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var t = play.Target!;
        var dmg = DynamicVars.Damage.BaseValue;
        if (t.CurrentHp < t.MaxHp) dmg += BonusDamage;
        await DamageCmd.Attack(dmg).FromCard(this).Targeting(t)
            .WithAttackerAnim(HeathcliffAttackAnim.PickFor(this), HeathcliffAttackAnim.PickHitDelayFor(this))
            .WithHitFx("vfx/vfx_attack_slash").Execute(ctx);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(UpgradeDamageBy);
}
