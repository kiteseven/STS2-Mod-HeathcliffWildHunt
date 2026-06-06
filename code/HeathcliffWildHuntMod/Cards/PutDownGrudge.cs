using System.Collections.Generic;
using System.Threading.Tasks;
using HeathcliffWildHuntMod.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HeathcliffWildHuntMod.Cards;

/// <summary>C5 沉沦之刺：1费攻击7(9)，给目标1层4(5)强度沉沦。沉沦定位：高强度。</summary>
public sealed class PutDownGrudge : CardModel
{
    private const decimal BaseDamage = 7m;
    private const decimal UpgradeDamageBy = 2m;
    private const int SinkingLayers = 1;
    private const int IntensityBase = 4;
    private const int IntensityUp = 1;
    private int _intensity = IntensityBase;

    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Strike };
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new DamageVar(BaseDamage, ValueProp.Move) };

    public PutDownGrudge() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var t = play.Target!;
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this).Targeting(t)
            .WithAttackerAnim(HeathcliffAttackAnim.PickFor(this), HeathcliffAttackAnim.PickHitDelayFor(this))
            .WithHitFx("vfx/vfx_attack_slash").Execute(ctx);
        // 沉沦：加 SinkingLayers 层 + _intensity 强度（纯累加，多卡叠加会累积）
        await SinkingPower.Apply(ctx, t, layers: SinkingLayers, intensity: _intensity, base.Owner.Creature, this);
    }

    protected override void OnUpgrade() { DynamicVars.Damage.UpgradeValueBy(UpgradeDamageBy); _intensity += IntensityUp; }
}
