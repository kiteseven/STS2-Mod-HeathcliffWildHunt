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

/// <summary>C3 荒野挥砍：1费攻击12(15)+给目标3层2(3)强度沉沦。沉沦定位：中等层数中等强度。</summary>
public sealed class WildSwing : CardModel
{
    private const decimal BaseDamage = 12m;
    private const decimal UpgradeDamageBy = 3m;
    private const int LayersBase = 3;
    private const int IntensityBase = 2;
    private const int IntensityUp = 1;

    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Strike };
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new DamageVar(BaseDamage, ValueProp.Move) };

    private int _intensity = IntensityBase;
    public WildSwing() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var target = play.Target!;
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this).Targeting(target)
            .WithAttackerAnim(HeathcliffAttackAnim.PickFor(this), HeathcliffAttackAnim.PickHitDelayFor(this))
            .WithHitFx("vfx/vfx_attack_slash").Execute(ctx);
        // 沉沦：加 LayersBase 层 + _intensity 强度（纯累加）
        await SinkingPower.Apply(ctx, target, layers: LayersBase, intensity: _intensity, base.Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(UpgradeDamageBy);
        _intensity += IntensityUp;
    }
}
