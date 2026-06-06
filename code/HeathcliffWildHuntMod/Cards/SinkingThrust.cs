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

/// <summary>U8 沉沦突刺：1费攻击8(11)，目标每层沉沦额外造成一次伤害。</summary>
public sealed class SinkingThrust : CardModel
{
    private const decimal BaseDamage = 8m;
    private const decimal UpgradeDamageBy = 3m;

    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Strike };
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new DamageVar(BaseDamage, ValueProp.Move) };

    public SinkingThrust() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var target = play.Target!;
        var sinkLayers = target.GetPowerAmount<SinkingPower>();
        var dmg = DynamicVars.Damage.BaseValue;

        // 基础攻击
        await DamageCmd.Attack(dmg).FromCard(this).Targeting(target)
            .WithAttackerAnim(HeathcliffAttackAnim.PickFor(this), HeathcliffAttackAnim.PickHitDelayFor(this))
            .WithHitFx("vfx/vfx_attack_slash").Execute(ctx);

        // 每层沉沦额外造成一次伤害
        for (int i = 0; i < sinkLayers; i++)
        {
            await DamageCmd.Attack(dmg).FromCard(this).Targeting(target)
                .WithAttackerAnim(HeathcliffAttackAnim.PickFor(this), HeathcliffAttackAnim.PickHitDelayFor(this))
                .WithHitFx("vfx/vfx_attack_slash").Execute(ctx);
        }
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(UpgradeDamageBy);
}
