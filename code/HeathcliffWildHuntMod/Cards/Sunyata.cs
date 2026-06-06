using System.Collections.Generic;
using System.Threading.Tasks;
using HeathcliffWildHuntMod.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace HeathcliffWildHuntMod.Cards;

/// <summary>
/// EGO「空即是色 Ya Śūnyatā Tad Rūpam」：1费攻击（占位伤害9），释放占位扣 25 理智。
/// <para>
/// 效果：[攻击前] 若自身理智 ≥ 15，本次攻击段数 +2（占位多段）；给目标 2 虚弱；恢复 25 理智。
/// </para>
/// </summary>
public sealed class Sunyata : EgoCardBase
{
    private const decimal BaseDamage = 9m;
    private const int BaseHits = 1;
    private const int BonusHits = 2;
    private const int WeakStacks = 2;
    private const int SanityRestore = 25;
    private const int HighSanityThreshold = 15;

    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Strike };
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust, CardKeyword.Ethereal };
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new DamageVar(BaseDamage, ValueProp.Move) };

    public Sunyata() : base(1, CardType.Attack, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var o = base.Owner.Creature;
        // [攻击前] 先按「扣释放成本之前」的理智判定段数加成（≥15 → +2 段），再扣占位理智成本。
        // 这样「攻击前 if 理智≥15」语义不被本卡自身的释放成本反噬。
        bool highSanity = (o.GetPower<SanityPower>()?.Amount ?? 0) >= HighSanityThreshold;
        int hits = BaseHits + (highSanity ? BonusHits : 0);
        await SanityPower.Drain(ctx, o, 25, o, this);

        var target = play.Target!;
        for (int i = 0; i < hits; i++)
        {
            if (!target.IsAlive) break;
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this).Targeting(target)
                .WithAttackerAnim(HeathcliffAttackAnim.PickFor(this), HeathcliffAttackAnim.PickHitDelayFor(this))
                .WithHitFx("vfx/vfx_attack_slash").Execute(ctx);
        }

        if (target.IsAlive)
            await PowerCmd.Apply<WeakPower>(ctx, target, WeakStacks, o, this);
        // 恢复 25 理智（空即是色——以攻代守，平复心神）
        await SanityPower.Restore(ctx, o, SanityRestore, o, this);
    }
}
