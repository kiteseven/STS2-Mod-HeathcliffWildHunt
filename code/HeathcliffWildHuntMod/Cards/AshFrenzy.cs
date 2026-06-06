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

/// <summary>
/// 崩溃，哀嚎与悲叹 Collapse, Wail and Lament（类名沿用 AshFrenzy）：1 费攻击 6(8)，本场每消耗过 1 张牌额外 +2 伤害（最多 +16）。消耗。
/// <para>
/// 烧牌手段（消耗方向）样例之二：展示"消耗 archetype 闭环"——伤害随本场累计消耗数 scaling，
/// 计数由 <see cref="HeathcliffStatePower.CardsExhaustedThisCombat"/> 提供，奖励玩家围绕消耗构筑。
/// 本卡自身也带消耗，打出后自加 1 计数（结算伤害用打出前的快照，不含本卡）。
/// </para>
/// </summary>
public sealed class AshFrenzy : CardModel
{
    private const decimal BaseDamage = 6m;
    private const decimal UpgradeDamageBy = 2m;
    private const decimal ExtraPerExhaust = 2m;
    private const decimal MaxExtra = 16m;

    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Strike };
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new DamageVar(BaseDamage, ValueProp.Move) };

    public AshFrenzy() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var owner = base.Owner.Creature;
        int exhausted = owner.GetPower<HeathcliffStatePower>()?.CardsExhaustedThisCombat ?? 0;
        decimal extra = System.Math.Min(MaxExtra, exhausted * ExtraPerExhaust);
        var dmg = DynamicVars.Damage.BaseValue + extra;

        await DamageCmd.Attack(dmg).FromCard(this).Targeting(play.Target!)
            .WithAttackerAnim(HeathcliffAttackAnim.PickFor(this), HeathcliffAttackAnim.PickHitDelayFor(this))
            .WithHitFx("vfx/vfx_attack_slash").Execute(ctx);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(UpgradeDamageBy);
}
