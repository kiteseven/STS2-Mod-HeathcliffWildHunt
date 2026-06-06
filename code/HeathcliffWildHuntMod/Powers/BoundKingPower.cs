using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HeathcliffWildHuntMod.Powers;

/// <summary>
/// 缚王御前 BoundKing（玩家 buff，可叠层）。
/// <para>
/// 效果：当<b>场上只剩一个可攻击的敌人</b>时，本方造成 Move 伤害后<b>额外追加一次</b>等量打击
/// （每层缚王御前追加一次）。设计意图——在单体残局把火力翻倍，呼应「缚王御前」的处决意象。
/// </para>
/// <para>
/// 防递归：追加打击本身也是 Move 伤害，会再次进 <see cref="AfterDamageGiven"/>，
/// 用 <see cref="_processing"/> 标记拦住，避免无限循环。
/// </para>
/// </summary>
public sealed class BoundKingPower : PowerModel
{
    // 追加打击的再入守卫：处理追加伤害期间，忽略它自己触发的 AfterDamageGiven
    private bool _processing;

    public override PowerType Type => PowerType.Buff;

    // 可叠层：层数 = 额外追加打击次数
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// 本方造成 Move 伤害后：若场上仅剩 1 个可攻击敌人，按层数追加等量打击。
    /// </summary>
    public override async Task AfterDamageGiven(
        PlayerChoiceContext ctx, Creature? dealer, DamageResult result,
        ValueProp props, Creature target, CardModel? cardSource)
    {
        if (dealer != base.Owner) return;
        if (!props.HasFlag(ValueProp.Move)) return;
        if (_processing) return;
        if (base.Amount <= 0) return;

        // 仅在单体残局触发：可攻击敌人数恰为 1
        var enemies = base.Owner.CombatState?.HittableEnemies;
        if (enemies is null || enemies.Count != 1) return;

        // 追加打击量 = 本次实际造成的总伤害（被格挡+真伤）。无伤害则不追加。
        int extra = result.TotalDamage;
        if (extra <= 0) return;

        _processing = true;
        try
        {
            // 每层缚王御前追加一次等量打击（无视格挡判定走 vanilla 正常伤害管线）
            for (int i = 0; i < base.Amount; i++)
            {
                if (!target.IsAlive) break;
                await CreatureCmd.Damage(ctx, target, extra, ValueProp.Move, base.Owner, cardSource);
            }
        }
        finally
        {
            _processing = false;
        }
    }
}
