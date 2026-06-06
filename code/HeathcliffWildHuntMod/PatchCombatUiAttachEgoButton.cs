using HarmonyLib;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Nodes.Combat;
using HeathcliffWildHuntMod.Combat.Ui;
using HeathcliffWildHuntMod.EgoPile;

namespace HeathcliffWildHuntMod;

/// <summary>
/// 在 NCombatUi 激活时，为拥有 EGO 框架的玩家挂载 EGO 主动释放按钮（能量球上方）。
/// </summary>
[HarmonyPatch(typeof(NCombatUi), nameof(NCombatUi.Activate))]
internal static class PatchCombatUiAttachEgoButton
{
    [HarmonyPostfix]
    private static void Postfix(NCombatUi __instance, CombatState combatState)
    {
        // 获取当前玩家
        var player = combatState?.HumanPlayer?.Player;
        if (player is null) return;

        // owner 门禁：只有 EGO 框架的拥有者才显示按钮
        if (!EgoFramework.IsOwnerPlayer(player)) return;

        // 获取能量球容器（公开属性）
        var energyContainer = __instance.EnergyCounterContainer;
        if (energyContainer is null) return;

        // 幂等检测：已存在则跳过（Activate 可能多次调用）
        if (energyContainer.HasNode("EgoReleaseButton")) return;

        // 创建并挂载 EGO 释放按钮
        var button = new NEgoReleaseButton(player);
        energyContainer.AddChild(button);
    }
}
