using System.Collections.Generic;
using System.Linq;
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
/// EGO「拘束 Binds」：1费 AOE 攻击（占位伤害14），释放占位扣 0 理智（释放门槛最低）。
/// <para>
/// 效果：对所有敌人造成中等伤害；[攻击后] 3 回合内每回合末扣 10 理智；
/// 自身获得 1 缚王御前 + 2 力量；给目标 2 易伤；给目标 4 层 + 4 强度沉沦。
/// </para>
/// </summary>
public sealed class Binds : EgoCardBase
{
    private const decimal BaseDamage = 14m;
    private const int CountdownTurns = 3;
    private const int BoundKingGain = 1;
    private const int StrengthGain = 2;
    private const int VulnerableStacks = 2;
    private const int SinkingLayers = 4;
    private const int SinkingIntensity = 4;

    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Strike };
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust, CardKeyword.Ethereal };
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new DamageVar(BaseDamage, ValueProp.Move) };

    public Binds() : base(1, CardType.Attack, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var o = base.Owner.Creature;
        // 占位理智成本 0：拘束是最低门槛 EGO（仍走 OnPlay 以保持「确认打出才生效」一致性）
        await SanityPower.Drain(ctx, o, 0, o, this);

        var target = play.Target!;
        var combatState = target.CombatState!;
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this).TargetingAllOpponents(combatState)
            .WithAttackerAnim(HeathcliffAttackAnim.PickFor(this), HeathcliffAttackAnim.PickHitDelayFor(this))
            .WithHitFx("vfx/vfx_attack_slash").Execute(ctx);

        // [攻击后] 自身：3 回合理智枷锁 + 1 缚王御前 + 2 力量
        await PowerCmd.Apply<BindsCountdownPower>(ctx, o, CountdownTurns, o, this);
        await PowerCmd.Apply<BoundKingPower>(ctx, o, BoundKingGain, o, this);
        await PowerCmd.Apply<StrengthPower>(ctx, o, StrengthGain, o, this);

        // 给主目标 2 易伤 + 4 层 4 强度沉沦（目标可能已阵亡，先判存活）
        if (target.IsAlive)
        {
            await PowerCmd.Apply<VulnerablePower>(ctx, target, VulnerableStacks, o, this);
            await SinkingPower.Apply(ctx, target, layers: SinkingLayers, intensity: SinkingIntensity, o, this);
        }
    }
}
