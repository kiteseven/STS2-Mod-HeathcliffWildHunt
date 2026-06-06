using System.Collections.Generic;
using HarmonyLib;
using HeathcliffWildHuntMod.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Relics;

namespace HeathcliffWildHuntMod;

/// <summary>
/// 欧洛巴斯(Orobas)事件用远古之牙 <see cref="ArchaicTooth"/> 做「升华(transcendence)」：把牌组里的某张
/// 基础卡升华成对应先古卡。vanilla 的映射表 <c>TranscendenceUpgrades</c> 是 private static 属性，硬编码
/// 5 组 vanilla 映射(Bash→Break 等)。这里 Postfix 注入本 mod 的映射：
/// <c>把你也装进这棺材(IntoThisCoffin) → 镇魂曲(Requiem)</c>。
/// <para>
/// 连带效果(正面)：<c>ArchaicTooth.TranscendenceCards</c> 派生自本字典的 values，注入后 Requiem 会被
/// 达弗(Darv)的尘封魔典 <c>DustyTome.SetupForPlayer</c> 的 <c>!TranscendenceCards.Contains</c> 过滤掉，
/// 从而镇魂曲只走欧洛巴斯升华、不与达弗的先古卡(觉悟)抢同一个池子。
/// </para>
/// <para>仿现有 <see cref="PatchTouchOfOrobas"/> 的 Postfix(ref Dictionary) 注入手法。</para>
/// </summary>
[HarmonyPatch(typeof(ArchaicTooth), "get_TranscendenceUpgrades")]
internal static class PatchArchaicToothTranscendence
{
    private static void Postfix(ref Dictionary<ModelId, CardModel> __result)
    {
        var starterId = ModelDb.Card<IntoThisCoffin>().Id;
        if (!__result.ContainsKey(starterId))
            __result[starterId] = ModelDb.Card<Requiem>();
    }
}
