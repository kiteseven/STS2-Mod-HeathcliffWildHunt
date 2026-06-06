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

/// <summary>R31 玻璃碎片：1费攻击7(10)，每层杜拉罕额外造成一次伤害（从原来的+伤害改为追加攻击）。</summary>
public sealed class GlassShards : CardModel
{
    private const decimal BaseDamage = 7m;
    private const decimal UpgradeDamageBy = 3m;

    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Strike };
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new DamageVar(BaseDamage, ValueProp.Move) };

    public GlassShards() : base(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var t = play.Target!;
        var dmg = DynamicVars.Damage.BaseValue;
        int dullahan = base.Owner.Creature.GetPowerAmount<DullahanPower>();

        // 基础攻击
        await DamageCmd.Attack(dmg).FromCard(this).Targeting(t)
            .WithAttackerAnim(HeathcliffAttackAnim.PickFor(this), HeathcliffAttackAnim.PickHitDelayFor(this))
            .WithHitFx("vfx/vfx_attack_slash").Execute(ctx);

        // 每层杜拉罕追加一次伤害
        for (int i = 0; i < dullahan; i++)
        {
            await DamageCmd.Attack(dmg).FromCard(this).Targeting(t)
                .WithAttackerAnim(HeathcliffAttackAnim.PickFor(this), HeathcliffAttackAnim.PickHitDelayFor(this))
                .WithHitFx("vfx/vfx_attack_slash").Execute(ctx);
        }
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(UpgradeDamageBy);
}
