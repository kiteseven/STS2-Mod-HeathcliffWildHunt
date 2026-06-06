using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HeathcliffWildHuntMod.Cards;

/// <summary>C12 该赎罪了：1费(0费+)攻击6(9)+给目标2易伤+2虚弱(3易伤+3虚弱+)。</summary>
public sealed class AtoneForSin : CardModel
{
    private const decimal BaseDamage = 6m;
    private const decimal UpgradeDamageBy = 3m;
    private const int VulnBase = 2;
    private const int VulnUp = 1;
    private const int WeakBase = 2;
    private const int WeakUp = 1;
    private int _vuln = VulnBase;
    private int _weak = WeakBase;
    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Strike };
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new DamageVar(BaseDamage, ValueProp.Move) };

    public AtoneForSin() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this).Targeting(play.Target!)
            .WithAttackerAnim(HeathcliffAttackAnim.PickFor(this), HeathcliffAttackAnim.PickHitDelayFor(this))
            .WithHitFx("vfx/vfx_attack_slash").Execute(ctx);
        var t = play.Target!;
        await PowerCmd.Apply<VulnerablePower>(ctx, t, _vuln, base.Owner.Creature, this);
        await PowerCmd.Apply<WeakPower>(ctx, t, _weak, base.Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(UpgradeDamageBy);
        _vuln += VulnUp;
        _weak += WeakUp;
        EnergyCost.UpgradeBy(-1);
    }
}
