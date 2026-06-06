using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace HeathcliffWildHuntMod.Cards;

/// <summary>C11 击倒出局：1费攻击15(20)，给目标1(2)虚弱。</summary>
public sealed class Knockout : CardModel
{
    private const decimal BaseDamage = 15m;
    private const decimal UpgradeDamageBy = 5m;
    private const int WeakBase = 1;
    private const int WeakUp = 1;
    private int _weak = WeakBase;

    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Strike };
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new DamageVar(BaseDamage, ValueProp.Move) };

    public Knockout() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var t = play.Target!;
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this).Targeting(t)
            .WithAttackerAnim(HeathcliffAttackAnim.PickFor(this), HeathcliffAttackAnim.PickHitDelayFor(this))
            .WithHitFx("vfx/vfx_attack_slash").Execute(ctx);
        await PowerCmd.Apply<WeakPower>(ctx, t, _weak, base.Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(UpgradeDamageBy);
        _weak += WeakUp;
    }
}
