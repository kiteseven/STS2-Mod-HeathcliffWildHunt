using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves;

namespace HeathcliffWildHuntMod;

// =============================================================================
// ⚠⚠⚠  测试专用补丁 —— 发布前必须删除整个文件！  ⚠⚠⚠
// -----------------------------------------------------------------------------
// 用途：本地平衡测试时，让狂猎希斯克利夫在选人界面直接可选到 A10（飞升 10）难度，
//       免去先用本角色逐级通关解锁的麻烦。
//
// 原理（已对照 sts2Code-officialVersion 源码确认）：
//   单人模式下「某角色可选的最高飞升等级」唯一来源是存档里
//   CharacterStats.MaxAscension：
//     · StartRunLobby.SetSingleplayerAscensionAfterCharacterChanged 读它喂给
//       NAscensionPanel.SetMaxAscension（选人界面飞升箭头上限）。
//     · StartRunLobby（开局）再用它 Math.Min 夹一次实际开局飞升。
//   狂猎是 mod 角色，IsAscensionEpochRevealed 对非 vanilla 角色默认返回 true，
//   所以飞升 epoch 这道门对狂猎天然放行——只要 MaxAscension>0 就能选。
//   官方控制台 `unlock ascensions` 做的也正是把每个角色 MaxAscension 置 10
//   （UnlockConsoleCmd.cs:187）。
//
// 做法：Postfix 钩 ProgressState.GetOrCreateCharacterStats——每次有人取狂猎的
//       角色存档统计时，把它的 MaxAscension 抬到 10。纯内存抬升，逻辑上等价于
//       官方解锁；不写盘也能在本次进程内让选人界面显示满级飞升可选。
//
// 影响面：只动 Heathcliff 自己的 CharacterStats，不碰任何 vanilla 角色。
// 可逆性：删掉本文件重新编译即可彻底移除，存档不会被本补丁强制写入（仅当游戏
//         自身在别处 SaveProgressFile 时才会把抬升后的值落盘，正常测试流程可接受）。
// =============================================================================
[HarmonyPatch(typeof(ProgressState), nameof(ProgressState.GetOrCreateCharacterStats))]
internal static class PatchUnlockHeathcliffAscension_TESTONLY
{
    // 飞升上限（与官方 AscensionManager.maxAscensionAllowed 一致）
    private const int UnlockToAscension = 10;

    private static void Postfix(ModelId characterId, CharacterStats __result)
    {
        // 仅对狂猎希斯克利夫生效；其它角色原样返回。
        if (characterId != ModelDb.GetId<Heathcliff>()) return;

        // 已是 10 就不动，避免无谓写入；否则抬到满级飞升。
        if (__result.MaxAscension < UnlockToAscension)
            __result.MaxAscension = UnlockToAscension;
    }
}
