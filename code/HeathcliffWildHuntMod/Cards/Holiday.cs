using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HeathcliffWildHuntMod.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HeathcliffWildHuntMod.Cards;

/// <summary>
/// EGO「悲惨假日 Holiday」：1费攻击（占位伤害18），释放占位扣 20 理智。
/// <para>
/// 效果：对主目标造成伤害；若<b>主目标理智 ≥ 15</b>，额外随机攻击一个敌人；
/// 主目标失去 10 理智；给主目标 7 层 + 7 强度沉沦。
/// </para>
/// </summary>
public sealed class Holiday : EgoCardBase
{
    private const decimal BaseDamage = 18m;
    private const int TargetSanityDrain = 10;
    private const int SinkingLayers = 7;
    private const int SinkingIntensity = 7;
    private const int HighSanityThreshold = 15;

    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Strike };
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust, CardKeyword.Ethereal };
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new DamageVar(BaseDamage, ValueProp.Move) };

    public Holiday() : base(1, CardType.Attack, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var o = base.Owner.Creature;
        // 占位理智成本：放 OnPlay 开头扣，确保「确认打出」才扣（取消选目标不入队 → 不扣）
        await SanityPower.Drain(ctx, o, 20, o, this);

        var target = play.Target!;
        // 主目标理智 ≥ 15 → 额外随机打一个敌人（先判定，避免攻击后目标侧 CombatState 被清空）
        bool highSanity = (target.GetPower<SanityPower>()?.Amount ?? 0) >= HighSanityThreshold;

        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this).Targeting(target)
            .WithAttackerAnim(HeathcliffAttackAnim.PickFor(this), HeathcliffAttackAnim.PickHitDelayFor(this))
            .WithHitFx("vfx/vfx_attack_slash").Execute(ctx);

        if (highSanity)
        {
            // 用施法者侧 CombatState 取存活敌人，随机挑一个追加打击
            var enemies = o.CombatState?.HittableEnemies;
            if (enemies is not null && enemies.Count > 0)
            {
                var extra = base.Owner.RunState.Rng.CombatTargets.NextItem(enemies);
                if (extra is not null)
                    await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this).Targeting(extra)
                        .WithAttackerAnim(HeathcliffAttackAnim.PickFor(this), HeathcliffAttackAnim.PickHitDelayFor(this))
                        .WithHitFx("vfx/vfx_attack_slash").Execute(ctx);
            }
        }

        // 主目标失去 10 理智 + 加 7 层 7 强度沉沦（目标可能已阵亡，先判存活）
        if (target.IsAlive)
        {
            await SanityPower.Drain(ctx, target, TargetSanityDrain, o, this);
            await SinkingPower.Apply(ctx, target, layers: SinkingLayers, intensity: SinkingIntensity, o, this);
        }
    }
}
