using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HeathcliffWildHuntMod.Powers;

/// <summary>浮士德随从被动：存活期间，每回合结束时给予所有敌人 1 层沉沦。</summary>
public sealed class FaustBuffPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.None;

    /// <summary>玩家回合结束时，给予所有存活的敌人 1 层沉沦。</summary>
    // 测试版(4-28)与正式版 dll 实测都用 BeforeTurnEnd(ctx, side)（无 participants 形参）；
    // 反编译源码树里残留的 BeforeSideTurnEnd 与实际 dll 不符，故统一 BeforeTurnEnd，participants 由结束方一侧 creature 重建。
    public override async Task BeforeTurnEnd(PlayerChoiceContext ctx, CombatSide side)
    {
        var participants = base.Owner.CombatState?.GetCreaturesOnSide(side) ?? (IReadOnlyList<Creature>)System.Array.Empty<Creature>();
        // 随从是怪物型 Creature，Player 永远为 null；必须用 PetOwner 追溯到所属玩家
        // Hook.BeforeTurnEnd 传入的 participants 只含玩家本体 Creature，不含随从，所以需要检查 PetOwner
        var playerCreature = base.Owner.PetOwner?.Creature;
        if (playerCreature == null || !participants.Contains(playerCreature)) return;
        var enemies = base.Owner.CombatState.Creatures.Where(c => c.IsEnemy && c.IsAlive);
        // 各敌人加 2 层沉沦 + 1 强度（统一累加入口；自动+1强度已移除，需显式给强度）
        foreach (var e in enemies) await SinkingPower.Apply(ctx, e, layers: 2, intensity: 1, base.Owner, null);
    }
}
