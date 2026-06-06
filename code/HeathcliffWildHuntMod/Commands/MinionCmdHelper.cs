using System.Linq;
using System.Threading.Tasks;
using HeathcliffWildHuntMod.Monsters;
using HeathcliffWildHuntMod.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HeathcliffWildHuntMod.Commands;

/// <summary>狂猎随从召唤辅助（自实现，不引用前置库 MinionLib）。封装查重 + AddPet + 召唤后钩子。</summary>
public static class MinionCmdHelper
{
    /// <summary>
    /// 召唤一个随从到玩家侧。返回当前这只随从的 Creature。
    /// <para>
    /// <b>查重规则</b>：同类随从场上已存在（存活）时<b>不重复召唤</b>，改为给已有随从增加生命上限
    /// （<see cref="CreatureCmd.GainMaxHp"/> 会同时回满涨幅，可超原始上限），涨幅 = 该随从初始上限，
    /// 返回已存在的那只随从。
    /// </para>
    /// </summary>
    /// <param name="isUpgraded">卡牌是否已升级——在 OnSummon 之前写入，确保召唤钩子能读到正确的升级状态。</param>
    public static async Task<Creature> Summon<T>(Player player, bool isUpgraded = false, CardModel? source = null) where T : MonsterModel
    {
        // ── 查重：场上已有同类存活随从 → 改为加血（可超原始上限），不新建 ──
        var existing = player.PlayerCombatState.Pets
            .FirstOrDefault(p => p.IsAlive && p.Monster is T);
        if (existing != null)
        {
            int hpGain = existing.Monster?.MaxInitialHp ?? 1; // 涨幅 = “再加一只的血量”
            await CreatureCmd.GainMaxHp(existing, hpGain);
            return existing;
        }

        // ── 首次召唤：创建新随从 ──
        var pet = await PlayerCmd.AddPet<T>(player);
        if (pet.Monster is WildHuntMinionModel minion)
        {
            // 必须在 OnSummon 之前设置 IsUpgraded，否则 OnSummon 里的升级 HP 逻辑读到的一直是 false
            minion.IsUpgraded = isUpgraded;
            await minion.OnSummon(player, pet, source);
            // 代主人受伤：挂自实现的护卫 power（MinionGuardPower 不重写"留场"，随从死亡正常移除立绘）。
            // 多只随从各挂一层，伤害天然链到第一只存活的随从。
            await PowerCmd.Apply<MinionGuardPower>(new ThrowingPlayerChoiceContext(), pet, 1, player.Creature, source);
        }
        return pet;
    }
}
