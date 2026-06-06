using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using HeathcliffWildHuntMod.Audio;

namespace HeathcliffWildHuntMod;

/// <summary>
///     为希斯克利夫玩家在播放动画 trigger 时附带播放 mod 自带音效。
///     攻击牌经 AttackCommand.WithAttackerAnim → <see cref="CreatureCmd.TriggerAnim" /> 播放动画，
///     这里用 Prefix <b>附加</b>播放对应音效（不跳过原版，动画照常触发）。
///
///     攻击按 trigger 选池（池内随机一个系列、系列内顺序连播）：
///     Attack→基础池、Attack2→中级池、Attack3/Attack4→重型池（3 与 4 合并随机）、Hit→受击。
///     注意：Cast 不在这里——能力牌不调 TriggerAnim，cast 由 PatchCardPlayCastSfx 在卡牌打出时挂。
///     杜拉罕形态与常态用相同攻击音效（按用户要求）。
/// </summary>
[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.TriggerAnim))]
internal static class PatchCreatureTriggerAnimSfx
{
    /// <summary>Harmony 前缀：仅为希斯克利夫玩家附加 mod 音效，恒返回 true 放行原版动画逻辑。</summary>
    private static bool Prefix(Creature creature, string triggerName)
    {
        // 只处理希斯克利夫玩家自己；其它角色/怪物一律不碰
        if (creature is not { IsPlayer: true } || creature.Player?.Character is not Heathcliff)
            return true;
        if (creature.IsDead)
            return true; // 已死亡不补音效（死亡音效另走 NCreature → dead）

        switch (triggerName)
        {
            // ── 攻击挥击音暂时停用：与动画时序对不上，等动画/音效节点对齐后再恢复 ──
            // case "Attack":
            //     WildHuntAudioService.PlayAttackPool(WildHuntSfx.AttackBasicPool); // 基础攻击
            //     break;
            // case "Attack2":
            //     WildHuntAudioService.PlayAttackPool(WildHuntSfx.AttackMidPool); // 中级
            //     break;
            // case "Attack3":
            // case "Attack4":
            //     // 重型（3+4合并随机）；这一系列偏慢，加倍速播放（pitch=2）
            //     WildHuntAudioService.PlayAttackPool(WildHuntSfx.AttackHeavyPool, 2.0f);
            //     break;
            case "Hit":
                // 受击：播放中不重叠——上一声受击音没播完就不起新的（敌人一轮多段攻击只响一次）
                WildHuntAudioService.PlayOneShotNoOverlap(WildHuntSfx.Hurt, "hurt");
                break;
        }

        return true; // 放行原版：动画照常触发
    }
}
