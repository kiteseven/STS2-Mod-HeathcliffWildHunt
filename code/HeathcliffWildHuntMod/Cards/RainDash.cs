using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HeathcliffWildHuntMod.Cards;

/// <summary>C7 雨夜疾驰：1费攻击8(11)，如果敌人本回合受到伤害则额外造成一次伤害。</summary>
public sealed class RainDash : CardModel
{
    private const decimal BaseDamage = 8m;
    private const decimal UpgradeDamageBy = 3m;
    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Strike };
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] {
        new DamageVar(BaseDamage, ValueProp.Move),
    };
    public RainDash() : base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy) { }
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var dmg = DynamicVars.Damage.BaseValue;
        var target = play.Target!;

        // 基础攻击
        await DamageCmd.Attack(dmg).FromCard(this).Targeting(target)
            .WithAttackerAnim(HeathcliffAttackAnim.PickFor(this), HeathcliffAttackAnim.PickHitDelayFor(this))
            .WithHitFx("vfx/vfx_attack_slash").Execute(ctx);

        // 判断目标本回合是否受到过伤害（当前HP < 最大HP）
        if (target.CurrentHp < target.MaxHp)
        {
            await DamageCmd.Attack(dmg).FromCard(this).Targeting(target)
                .WithAttackerAnim(HeathcliffAttackAnim.PickFor(this), HeathcliffAttackAnim.PickHitDelayFor(this))
                .WithHitFx("vfx/vfx_attack_slash").Execute(ctx);
        }
    }
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(UpgradeDamageBy);
}
