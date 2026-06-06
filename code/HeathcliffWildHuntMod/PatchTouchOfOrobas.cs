using System.Collections.Generic;
using HarmonyLib;
using HeathcliffWildHuntMod.Relics;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;

namespace HeathcliffWildHuntMod;

/// <summary>
/// TouchOfOrobas.RefinementUpgrades 硬编码了 5 个 vanilla Starter→Ancient 映射。
/// Postfix 构造时加入凯瑟琳棺材→删除凯茜，让 Ancient 事件能识别本 mod 的起始遗物交换。
/// </summary>
[HarmonyPatch(typeof(TouchOfOrobas), "get_RefinementUpgrades")]
internal static class PatchTouchOfOrobas
{
    private static void Postfix(ref Dictionary<ModelId, RelicModel> __result)
    {
        var coffinId = ModelDb.Relic<CatherineCoffinRelic>().Id;
        if (!__result.ContainsKey(coffinId))
            __result[coffinId] = ModelDb.Relic<CleanAllCathyRelic>();
    }
}
