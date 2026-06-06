using HarmonyLib;
using HeathcliffWildHuntMod.Powers;
using MegaCrit.Sts2.Core.Models;

namespace HeathcliffWildHuntMod;

/// <summary>
/// 让 <see cref="SanityPower"/> 永不因层数 0 / 负数被自动移除——
/// 理智是显性资源条，必须始终显示在能力栏上，不允许 vanilla 的"AllowNegative + Amount==0"逻辑把它清掉。
/// <para>
/// 背景（参考 vanilla 源码 <c>D:\STS2ModProject\Sts2Code\MegaCrit.Sts2.Core.Models\PowerModel.cs</c>）：
/// <code>
/// public bool ShouldRemoveDueToAmount()
/// {
///     if (AllowNegative || Amount > 0)
///     {
///         if (AllowNegative) return Amount == 0;   // ← 我们的 SanityPower 走这条，0 时被判定要移除
///         return false;
///     }
///     return true;
/// }
/// </code>
/// 不是 virtual 所以无法 override，只能用 Harmony Postfix 拦截。
/// </para>
/// </summary>
[HarmonyPatch(typeof(PowerModel), nameof(PowerModel.ShouldRemoveDueToAmount))]
internal static class PatchSanityNeverRemoved
{
    private static void Postfix(PowerModel __instance, ref bool __result)
    {
        // 仅对本 mod 的"理智"生效；其他 Power 保留 vanilla 行为
        if (__instance is SanityPower)
        {
            __result = false;
        }
    }
}
