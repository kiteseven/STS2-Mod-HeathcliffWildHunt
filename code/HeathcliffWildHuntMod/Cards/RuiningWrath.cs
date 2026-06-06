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

/// <summary>U7 破灭的怒火：1费攻击15(19)，自身理智>=10时伤害翻倍。</summary>
public sealed class RuiningWrath : CardModel
{
    private const decimal BaseDamage = 15m;
    private const decimal UpgradeDamageBy = 4m;
    private const int SanityThreshold = 10;

    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Strike };
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new DamageVar(BaseDamage, ValueProp.Move) };

    public RuiningWrath() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var damage = DynamicVars.Damage.BaseValue;
        var sanity = base.Owner.Creature.GetPower<SanityPower>();
        if (sanity is not null && sanity.Amount >= SanityThreshold)
            damage *= 2m;
        await DamageCmd.Attack(damage)
            .FromCard(this).Targeting(play.Target!)
            .WithAttackerAnim(HeathcliffAttackAnim.PickFor(this), HeathcliffAttackAnim.PickHitDelayFor(this))
            .WithHitFx("vfx/vfx_attack_slash").Execute(ctx);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(UpgradeDamageBy);
}
