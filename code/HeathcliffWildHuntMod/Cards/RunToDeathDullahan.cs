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

/// <summary>R1 奔向死亡吧，杜拉罕啊：3费攻击36(48)，目标理智<0时+24(32)。</summary>
public sealed class RunToDeathDullahan : CardModel
{
    private const decimal BaseDamage = 28m;
    private const decimal UpgradeDamageBy = 10m;
    private const decimal BasePanicBonus = 18m;
    private const decimal UpgradePanicBonusBy = 8m;
    private const string PanicBonusVarKey = "PanicBonus";

    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Strike };
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[]
    {
        new DamageVar(BaseDamage, ValueProp.Move),
        new DamageVar(PanicBonusVarKey, BasePanicBonus, ValueProp.Move),
    };

    public RunToDeathDullahan() : base(3, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var target = play.Target!;
        var damage = DynamicVars.Damage.BaseValue;
        var sanity = target.GetPower<SanityPower>();
        if (sanity is not null && sanity.Amount < 0)
            damage += DynamicVars[PanicBonusVarKey].BaseValue;
        await DamageCmd.Attack(damage)
            .FromCard(this).Targeting(target)
            .WithAttackerAnim(HeathcliffAttackAnim.Attack3, HeathcliffAttackAnim.Attack3HitDelay)
            .WithHitFx("vfx/vfx_attack_slash").Execute(ctx);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(UpgradeDamageBy);
        DynamicVars[PanicBonusVarKey].UpgradeValueBy(UpgradePanicBonusBy);
    }
}
