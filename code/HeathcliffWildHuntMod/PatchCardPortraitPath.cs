using System.Linq;
using Godot;
using HarmonyLib;
using HeathcliffWildHuntMod.Cards;
using HeathcliffWildHuntMod.Relics;
using MegaCrit.Sts2.Core.Models;

namespace HeathcliffWildHuntMod;

/// <summary>
/// 所有狂猎卡的 PortraitPath 指向 images/cards/{ClassName}.png，
/// 解决 card_atlas 不认 mod 卡图的问题。
/// 用 Namespace 判断而非 c.Pool（Pool getter 有副作用，在 canonical 上下文中可能抛异常）。
/// </summary>
[HarmonyPatch(typeof(CardModel), "get_PortraitPath")]
internal static class PatchCardPortraitPath
{
    private static void Postfix(CardModel __instance, ref string __result)
    {
        if (__instance.GetType().Namespace != "HeathcliffWildHuntMod.Cards") return;

        // 先解析出该卡应使用的图名（默认=类名），再拼路径。
        var portraitName = ResolvePortraitName(__instance);
        var path = $"res://images/cards/{portraitName}.png";
        if (Godot.ResourceLoader.Exists(path))
            __result = path;
    }

    /// <summary>
    /// 返回卡图文件名（不含扩展名）。默认就是类名；个别卡按运行时状态切换变体图。
    /// </summary>
    private static string ResolvePortraitName(CardModel card)
    {
        var name = card.GetType().Name;

        // EGO 版 AEDD（类名 AeddEgo）复用普通卡 Aedd 的卡图（同一张 Aedd.png）。
        if (card is AeddEgo)
            return "Aedd";

        // 「凯瑟琳消失前/后」主题卡：玩家持有「抹除凯瑟琳」遗物(CleanAllCathyRelic) 时，
        // 换成抹消后的卡图，文件名约定 = {类名}-cleanAllcathy.png。
        // 目前只有 FadedPromise 有成品图，其余几张等素材补齐；ResolvePortraitName 外层用
        // ResourceLoader.Exists 兜底——变体图不存在时本方法返回的路径不会命中，自动回退默认卡图。
        if (card is FadedPromise or DecapitateHeathcliffCard or Lament or LoveAndHate or Requiem
            && HasCleanAllCathy(card))
        {
            return name + "-cleanAllcathy";
        }

        return name;
    }

    /// <summary>
    /// 玩家是否持有 CleanAllCathyRelic。
    /// ⚠️ Owner getter 内部 AssertMutable()，canonical（图鉴/卡池）实例会抛异常 → 必须先判 IsMutable。
    /// </summary>
    private static bool HasCleanAllCathy(CardModel card)
        => card.IsMutable
           && card.Owner?.Relics.Any(r => r is CleanAllCathyRelic) == true;
}
