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
/// EGO「凶弹 Fell Bullet」：1费 AOE 攻击（占位伤害12）×3 发，释放占位扣 15 理智。
/// <para>
/// 效果：对所有敌人造成 3 段高伤害；每段给目标加 3 层 + 3 强度沉沦。
/// </para>
/// </summary>
public sealed class FellBullet : EgoCardBase
{
    private const decimal BaseDamage = 12m;
    private const int Hits = 3;
    private const int SinkingLayersPerHit = 3;
    private const int SinkingIntensityPerHit = 3;

    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Strike };
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust, CardKeyword.Ethereal };
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new DamageVar(BaseDamage, ValueProp.Move) };

    public FellBullet() : base(1, CardType.Attack, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var o = base.Owner.Creature;
        await SanityPower.Drain(ctx, o, 15, o, this);

        var combatState = play.Target!.CombatState!;
        for (int i = 0; i < Hits; i++)
        {
            // 每发对全体 AOE
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this).TargetingAllOpponents(combatState)
                .WithAttackerAnim(HeathcliffAttackAnim.PickFor(this), HeathcliffAttackAnim.PickHitDelayFor(this))
                .WithHitFx("vfx/vfx_attack_slash").Execute(ctx);
            // 每发给所有存活敌人叠沉沦（用施法者侧 CombatState 取敌人，避免目标阵亡后 NRE）
            var enemies = o.CombatState?.Creatures.Where(c => c.IsEnemy && c.IsAlive).ToList();
            if (enemies is not null)
                foreach (var e in enemies)
                    await SinkingPower.Apply(ctx, e, layers: SinkingLayersPerHit, intensity: SinkingIntensityPerHit, o, this);
        }
    }
}
