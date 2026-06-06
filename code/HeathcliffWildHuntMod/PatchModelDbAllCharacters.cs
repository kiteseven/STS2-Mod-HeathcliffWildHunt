using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;

namespace HeathcliffWildHuntMod;

/// <summary>
/// 把 <see cref="Heathcliff"/> 注入到 <see cref="ModelDb.AllCharacters"/> 列表里。
/// <para>
/// 原版 <c>ModelDb.AllCharacters</c> 是 5 个角色的硬编码数组，外部 mod 不会被自动列出，
/// 因此选人界面 / 游戏其它地方都看不到本角色。Postfix 用 <c>Concat + Distinct</c> 追加我们的角色，
/// 与 RyoshuMod 等成熟 mod 的注入手法一致。
/// </para>
/// </summary>
[HarmonyPatch(typeof(ModelDb), "get_AllCharacters")]
internal static class PatchModelDbAllCharacters
{
    static PatchModelDbAllCharacters()
    {
        ModelDb.Inject(typeof(Heathcliff));
        ModelDb.Inject(typeof(Relics.CatherineCoffinRelic));
        ModelDb.Inject(typeof(Relics.CleanAllCathyRelic));
        ModelDb.Inject(typeof(RelicPools.HeathcliffRelicPool));
        // 狂猎随从——确保 MonsterModel 注册到 ModelDb
        ModelDb.Inject(typeof(Monsters.IshmaelMinion));
        ModelDb.Inject(typeof(Monsters.FaustMinion));
        ModelDb.Inject(typeof(Monsters.OutisMinion));
        ModelDb.Inject(typeof(Monsters.RyoshuMinion));
        ModelDb.Inject(typeof(Monsters.GregorMinion));
    }

    private static void Postfix(ref IEnumerable<CharacterModel> __result)
    {
        if (__result.Any(c => c is Heathcliff)) return;
        __result = __result.Append(ModelDb.Character<Heathcliff>()).ToArray();
    }
}
