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

/// <summary>B5 把你也装进这棺材：1费攻击6(9)，目标理智<0时+10(14)。</summary>
public sealed class IntoThisCoffin : CardModel
{
    private const decimal BaseDamage = 6m;
    private const decimal UpgradeDamageBy = 3m;
    private const decimal BasePanicBonus = 10m;
    private const decimal UpgradePanicBonusBy = 4m;
    private const string PanicBonusVarKey = "PanicBonus";

    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Strike };
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(BaseDamage, ValueProp.Move),
        new DamageVar(PanicBonusVarKey, BasePanicBonus, ValueProp.Move),
    };

    public IntoThisCoffin() : base(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var target = play.Target!;
        var damage = DynamicVars.Damage.BaseValue;
        var sanity = target.GetPower<SanityPower>();
        if (sanity is not null && sanity.Amount < 0)
            damage += DynamicVars[PanicBonusVarKey].BaseValue;
        await DamageCmd.Attack(damage)
            .FromCard(this).Targeting(target)
            .WithAttackerAnim(HeathcliffAttackAnim.PickFor(this), HeathcliffAttackAnim.PickHitDelayFor(this))
            .WithHitFx("vfx/vfx_attack_slash").Execute(ctx);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(UpgradeDamageBy);
        DynamicVars[PanicBonusVarKey].UpgradeValueBy(UpgradePanicBonusBy);
    }
}
