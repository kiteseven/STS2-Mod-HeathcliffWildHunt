using HarmonyLib;
using MegaCrit.Sts2.Core.Models;

namespace HeathcliffWildHuntMod;

/// <summary>
/// 把 <see cref="Heathcliff"/> 在选人界面按钮上显示的图标路径替换成 mod 自带的
/// <c>10710_normal_info.png</c>（用户提供的角色选择框图片）。
/// <para>
/// 原版 <see cref="CharacterModel.CharacterSelectIconPath"/> 默认拼成
/// <c>res://images/packed/character_select/char_select_heathcliff.png</c>，pck 里没这张图，
/// 走到 <c>NCharacterSelectButton.Init</c> 时 <c>character.CharacterSelectIcon</c> 解析失败按钮显示空白。
/// </para>
/// <para>
/// 修法：Postfix 把 getter 结果替换成 mod 资源目录下的实际 PNG 路径。
/// 与 RyoshuMod 中 <c>RyoshuCharSelectIconPatch</c> 思路一致（直接重定向 path getter，比改 _icon.Texture 更早一层）。
/// </para>
/// </summary>
[HarmonyPatch(typeof(CharacterModel), "get_CharacterSelectIconPath")]
internal static class PatchHeathcliffCharacterSelectIconPath
{
    // 用户提供的"角色选择框图片"，与 res://animations/character_select/Heathcliff-WildHunt/ 下的实际资源对齐
    private const string HeathcliffIconPath =
        "res://animations/character_select/Heathcliff-WildHunt/10710_normal_info.png";

    private static void Postfix(CharacterModel __instance, ref string __result)
    {
        if (__instance is not Heathcliff) return;

        __result = HeathcliffIconPath;
    }
}
