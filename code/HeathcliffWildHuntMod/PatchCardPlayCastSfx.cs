using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using HeathcliffWildHuntMod.Audio;

namespace HeathcliffWildHuntMod;

/// <summary>
///     希斯克利夫打出【能力牌】(CardType.Power) 时播放 cast 音效。
///     为什么单独 patch：能力牌的 OnPlay 不会调用 CreatureCmd.TriggerAnim("Cast")，
///     所以 <see cref="PatchCreatureTriggerAnimSfx" /> 那条路捕获不到 —— 这里从卡牌打出的统一入口
///     <see cref="CardModel.OnPlayWrapper" /> 挂，按 Type==Power 过滤。
///     按用户要求：仅能力牌播 cast，攻击/技能牌不播。
/// </summary>
[HarmonyPatch(typeof(CardModel), nameof(CardModel.OnPlayWrapper))]
internal static class PatchCardPlayCastSfx
{
    /// <summary>Harmony 前缀：能力牌且属于希斯克利夫玩家时播 cast；恒返回 true 放行原版。</summary>
    private static bool Prefix(CardModel __instance)
    {
        // 仅能力牌
        if (__instance.Type != CardType.Power)
            return true;
        // 仅希斯克利夫玩家打出的
        if (__instance.Owner?.Character is not Heathcliff)
            return true;

        WildHuntAudioService.PlayOneShot(WildHuntSfx.Cast);
        return true; // 放行原版打牌逻辑
    }
}
