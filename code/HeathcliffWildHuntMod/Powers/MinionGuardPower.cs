using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HeathcliffWildHuntMod.Powers;

/// <summary>
/// 随从护卫（狂猎自实现，<b>不引用</b>前置库的 <c>DieForYouPower</c>）。
/// <para>
/// 挂在随从身上：当玩家(PetOwner)将受到「攻击/移动」类未阻挡伤害时，把伤害重定向到本随从自身，替主人挡刀。
/// 多只随从各挂一层，天然链到第一只存活的随从。
/// </para>
/// <para>
/// ⚠️ 与 vanilla <c>DieForYouPower</c> 的关键区别：<b>不重写</b>
/// <c>ShouldCreatureBeRemovedFromCombatAfterDeath</c> 和 <c>ShouldPowerBeRemovedAfterOwnerDeath</c>。
/// vanilla 版把前者改成 false 让护卫死后「留场」——我们的随从不需要留场，沿用默认 true，
/// 死亡时正常 <c>QueueFree</c> 移除立绘节点（修复随从死亡后立绘残留的 bug）。
/// </para>
/// </summary>
public sealed class MinionGuardPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    /// <summary>纯机制 power，无图标特效。</summary>
    public override bool ShouldPlayVfx => false;

    /// <summary>
    /// 伤害重定向：目标是本随从的主人、本随从存活、且为「powered 攻击」伤害时，把承伤目标改成本随从。
    /// 其余情况（已死、非攻击伤害、目标不是主人）原样返回，不拦截。
    /// </summary>
    public override Creature ModifyUnblockedDamageTarget(Creature target, decimal _, ValueProp props, Creature? __)
    {
        if (target != base.Owner.PetOwner?.Creature) return target; // 只替主人挡
        if (base.Owner.IsDead) return target;                       // 自己已死则不再挡
        if (!props.IsPoweredAttack()) return target;                // 只挡攻击/移动类伤害
        return base.Owner;                                          // 重定向到随从自身
    }

    /// <summary>已死的随从不应再被选为攻击目标（链到下一只存活随从）。</summary>
    public override bool ShouldAllowHitting(Creature creature) => creature.IsAlive;
}
