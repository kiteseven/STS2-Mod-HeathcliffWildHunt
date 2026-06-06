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

/// <summary>U2 山庄的低咛：1费攻击8(11)，每有1棺+2伤害，首次使用+1棺(2棺+)，抽1(2+)。</summary>
public sealed class CoffinWhisper : CardModel
{
    private const decimal BaseDamage = 8m;
    private const decimal UpgradeDamageBy = 3m;
    private const int CoffinGainBase = 1;
    private const int CoffinGainUp = 1;
    private const int DrawBase = 1;
    private const int DrawUp = 1;
    private bool _firstUse = true;
    private int _coffinGain = CoffinGainBase;
    private int _draw = DrawBase;

    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Strike };
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new DamageVar(BaseDamage, ValueProp.Move) };

    public CoffinWhisper() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var o = base.Owner.Creature;
        var dmg = DynamicVars.Damage.BaseValue + o.GetPowerAmount<CoffinPower>() * 2;
        await DamageCmd.Attack(dmg).FromCard(this).Targeting(play.Target!)
            .WithAttackerAnim(HeathcliffAttackAnim.PickFor(this), HeathcliffAttackAnim.PickHitDelayFor(this))
            .WithHitFx("vfx/vfx_attack_slash").Execute(ctx);
        if (_firstUse)
        {
            await CoffinPower.Gain(ctx, o, _coffinGain, this);
            _firstUse = false;
        }
        await CardPileCmd.Draw(ctx, _draw, base.Owner);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(UpgradeDamageBy);
        _coffinGain += CoffinGainUp;
        _draw += DrawUp;
    }
}
