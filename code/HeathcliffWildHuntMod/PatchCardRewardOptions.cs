using HarmonyLib;
using MegaCrit.Sts2.Core.Runs;

namespace HeathcliffWildHuntMod;

/// <summary>
/// vanilla CardCreationOptions 的 AssertUniformOddsIfSingleRarityPool 在 mod 角色卡池的
/// 特定稀有度分布下会误判"只有一种稀有度"→ InvalidOperationException → 战后卡死。
/// Prefix 返回 false 直接跳过检查。
/// </summary>
[HarmonyPatch(typeof(CardCreationOptions), "AssertUniformOddsIfSingleRarityPool")]
internal static class PatchCardRewardOptions
{
    private static bool Prefix() => false;
}
