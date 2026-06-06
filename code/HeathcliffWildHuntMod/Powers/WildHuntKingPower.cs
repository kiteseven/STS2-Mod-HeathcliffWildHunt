using System;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HeathcliffWildHuntMod.Powers;

/// <summary>
/// R30 狂猎之王：每当玩家的随从阵亡时，恢复等同于其最大生命值一半的[理智]，升级后额外恢复四分之一血量。
/// 挂在玩家本体上，通过 <see cref="AfterDeath"/> 监听全场死亡事件，
/// 只对「PetOwner 是本 power 持有者」的随从生效。
/// </summary>
public sealed class WildHuntKingPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterDeath(
        PlayerChoiceContext choiceContext, Creature creature, bool wasRemovalPrevented, float deathAnimLength)
    {
        // 只处理「本玩家的随从」阵亡：随从的 PetOwner.Creature 应等于本 power 的持有者(玩家本体)。
        if (creature.PetOwner?.Creature != base.Owner) return;
        // 复活/阻止移除的不算真正阵亡，避免重复回理智。
        if (wasRemovalPrevented) return;

        int restoreSanity = (int)Math.Floor(creature.MaxHp / 2.0);
        if (restoreSanity > 0)
            await SanityPower.Restore(choiceContext, base.Owner, restoreSanity, base.Owner, null);

        // 升级后额外恢复玩家自己四分之一的随从最大血量（Amount > 1 表示升级）
        if (base.Amount > 1)
        {
            int restoreHp = (int)Math.Floor(creature.MaxHp / 4.0);
            if (restoreHp > 0)
                await CreatureCmd.Heal(base.Owner, restoreHp);
        }
    }
}
