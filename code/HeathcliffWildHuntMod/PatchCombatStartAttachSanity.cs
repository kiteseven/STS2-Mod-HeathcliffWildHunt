using HarmonyLib;
using HeathcliffWildHuntMod.EgoPile;
using HeathcliffWildHuntMod.Powers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace HeathcliffWildHuntMod;

/// <summary>
/// 战斗开始时附加本 mod 需要"始终在场"的 Power：
/// <list type="bullet">
///   <item><b><see cref="SanityPower"/></b>：由可复用的 <see cref="EgoFramework.AttachSanityIfOwnerRun"/> 负责——
///         双方都挂 0 层（显性资源条），但<b>仅当本局出战角色是本 mod 的 owner 角色时</b>才挂，
///         避免与共存的其它 EGO mod 重复附加理智条；</item>
///   <item><b><see cref="HeathcliffStatePower"/></b>：仅玩家挂一份（隐藏 Power，承载 OhDullahan
///         的 PendingDullahanAtTurnEnd 队列、本回合棺进出量、本场首次进入杜拉罕等跨卡状态）。
///         这是<b>希斯克利夫专属</b>逻辑，留在本 patch、不进框架。</item>
/// </list>
/// <para>
/// patch 入口：<see cref="CombatManager.StartCombatInternal"/> Postfix。
/// 选 Postfix 而非 Prefix —— vanilla 在该方法内部会先把所有 Creature 加入 _state.Creatures
/// （通过 <c>AfterCreatureAdded</c> 循环），再 await Hook.BeforeCombatStart。等 Postfix 跑时
/// 全部 creature 已经就绪，可以安全 Apply Power。
/// </para>
/// </summary>
[HarmonyPatch(typeof(CombatManager), nameof(CombatManager.StartCombatInternal))]
internal static class PatchCombatStartAttachSanity
{
    /// <summary>
    /// Postfix：fire-and-forget 一个异步任务把"始终在场"的 power 加上去。
    /// vanilla 的 PowerCmd.Apply 内部走 ActionExecutor 队列，所以即便 fire-and-forget 也不会乱序。
    /// </summary>
    private static void Postfix(CombatManager __instance)
    {
        // 复位狂猎 BGM 的"本场已触发"标志：每场战斗都允许在棺层首次达最大层时重新切歌
        HeathcliffWildHuntMod.Audio.WildHuntBgmController.ResetForCombat();

        // _state 是 CombatManager 的 private 字段，借助 AccessTools 拿 reflection
        // 双版本：测试版 _state 是 ICombatState；正式版删了该接口、_state 即 CombatState。
        // ICombatStateCompat 别名按渠道切换（见 Compat/CompatAliases.cs），两边都暴露 .Creatures。
        var state = AccessTools.Field(typeof(CombatManager), "_state").GetValue(__instance) as ICombatStateCompat;
        if (state == null) return;
        _ = AttachAllPersistentPowers(state);
    }

    private static async System.Threading.Tasks.Task AttachAllPersistentPowers(ICombatStateCompat state)
    {
        // 1) 理智：交给可复用框架——内部带 owner 门禁，非本 mod 出战角色时整段跳过
        await EgoFramework.AttachSanityIfOwnerRun(state);

        // 2) HeathcliffStatePower 仅玩家挂（隐藏 Power,只对本 mod 角色 Heathcliff 有意义）——希斯克利夫专属，不进框架
        var ctx = (PlayerChoiceContext)null!;
        foreach (var creature in state.Creatures)
        {
            if (creature == null) continue;
            // 判定"玩家"且角色是 Heathcliff:避免给敌人或其它角色玩家多挂一份隐藏 power
            if (creature.IsPlayer && creature.Player?.Character is Heathcliff && !creature.HasPower<HeathcliffStatePower>())
            {
                // StatePower.StackType=None,Apply 1 即落地；Amount 不参与计算,不需要清零
                await PowerCmd.Apply<HeathcliffStatePower>(ctx, creature, 1, creature, null);
            }
        }
    }
}
