using HarmonyLib;
using HeathcliffWildHuntMod.Powers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HeathcliffWildHuntMod;

/// <summary>
/// vanilla <see cref="ArtifactPower"/> 把所有 amount&lt;0 的 Counter+AllowNegative power 视为 Debuff，
/// 会消耗一层人工制品并把 amount 改成 0。<see cref="SanityPower"/> 是双向资源（理智既能扣也能加，
/// 跨零是常态），不应被人工制品 / 移除 debuff 类效果交互。
///
/// Prefix 拦截 ArtifactPower.TryModifyPowerAmountReceived：当目标 power 是 SanityPower 时直接放行，
/// 不消耗人工制品层数，amount 原样通过。
/// </summary>
[HarmonyPatch(typeof(ArtifactPower), nameof(ArtifactPower.TryModifyPowerAmountReceived))]
internal static class PatchArtifactIgnoresSanity
{
    // 参数名/顺序必须与 vanilla 完全一致：
    //   bool TryModifyPowerAmountReceived(PowerModel canonicalPower, Creature target,
    //       decimal amount, Creature? _, out decimal modifiedAmount)
    // Harmony 按参数名注入，漏掉第 4 个 Creature? _ 会导致 patch 绑定失败。
    private static bool Prefix(
        PowerModel canonicalPower, Creature target, decimal amount,
        Creature? _, out decimal modifiedAmount, ref bool __result)
    {
        if (canonicalPower is SanityPower)
        {
            modifiedAmount = amount;
            __result = false;
            return false;
        }
        modifiedAmount = default;
        return true;
    }
}
