using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HeathcliffWildHuntMod.Cards;

/// <summary>C9 狼牙挥扫：1费攻击AOE 12(12)。</summary>
public sealed class WolfSweep : CardModel
{
    private const decimal BaseDamage = 12m;
    private const decimal UpgradeDamageBy = 0m;
    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Strike };
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new DamageVar(BaseDamage, ValueProp.Move) };
    public WolfSweep() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
        => await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this)
            .TargetingAllOpponents(play.Target!.CombatState!)
            .WithAttackerAnim(HeathcliffAttackAnim.PickFor(this), HeathcliffAttackAnim.PickHitDelayFor(this))
            .WithHitFx("vfx/vfx_attack_slash").Execute(ctx);
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(UpgradeDamageBy);
}
