using System.Collections.Generic;
using System.Threading.Tasks;
using HeathcliffWildHuntMod.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace HeathcliffWildHuntMod.Cards;

/// <summary>R4 魔王所经之处：2费攻击9(13)×3，杜拉罕中×4+全体2易伤。</summary>
public sealed class DemonKingsPath : CardModel
{
    private const decimal BaseDamage = 9m;
    private const decimal UpgradeDamageBy = 4m;

    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Strike };
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new DamageVar(BaseDamage, ValueProp.Move) };

    public DemonKingsPath() : base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var hasDullahan = base.Owner.Creature.HasPower<DullahanPower>();
        int hits = hasDullahan ? 4 : 3;
        var dmg = DynamicVars.Damage.BaseValue;
        for (int i = 0; i < hits; i++)
            await DamageCmd.Attack(dmg).FromCard(this).Targeting(play.Target!)
                .WithAttackerAnim(HeathcliffAttackAnim.PickFor(this), HeathcliffAttackAnim.PickHitDelayFor(this))
                .WithHitFx("vfx/vfx_attack_slash").Execute(ctx);
        if (hasDullahan)
        {
            // 用施法者侧的 CombatState——目标可能在攻击中阵亡，目标侧 CombatState 会被清空导致 NRE
            var enemies = base.Owner.Creature.CombatState.HittableEnemies;
            foreach (var e in enemies)
                await PowerCmd.Apply<VulnerablePower>(ctx, e, 2, base.Owner.Creature, this);
        }
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(UpgradeDamageBy);
}
