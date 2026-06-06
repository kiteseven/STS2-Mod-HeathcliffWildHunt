using System.Linq;
using Godot;
using HarmonyLib;
using HeathcliffWildHuntMod.Monsters;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace HeathcliffWildHuntMod;

/// <summary>
/// vanilla NCombatRoom.AddCreature 把所有玩家宠物从 playerX - 20 开始向右铺，
/// 第一只就贴在玩家身上。狂猎随从需要离玩家远一点——重排所有 WildHuntMinionModel 宠物的 X。
/// </summary>
[HarmonyPatch(typeof(NCombatRoom), nameof(NCombatRoom.AddCreature))]
internal static class PatchWildHuntMinionLayout
{
    private const float ExtraOffsetX = 220f;
    private const float ExtraSpacing = 60f;

    private static void Postfix(NCombatRoom __instance, Creature creature)
    {
        if (creature.PetOwner == null) return;
        if (creature.Monster is not WildHuntMinionModel) return;

        var ownerNode = __instance.GetCreatureNode(creature.PetOwner.Creature);
        if (ownerNode == null) return;

        var minions = AccessTools.Field(typeof(NCombatRoom), "_creatureNodes")
            .GetValue(__instance) as System.Collections.Generic.List<NCreature>;
        if (minions == null) return;

        var wildHuntMinions = minions
            .Where(c => c.Entity.PetOwner == ownerNode.Entity.Player
                        && c.Entity.Monster is WildHuntMinionModel)
            .ToList();

        for (int i = 0; i < wildHuntMinions.Count; i++)
        {
            var node = wildHuntMinions[i];
            float halfWidth = node.Visuals.Bounds.Size.X * 0.5f;
            float x = ownerNode.Position.X + ExtraOffsetX + i * (halfWidth * 2f + ExtraSpacing);
            node.Position = new Vector2(x, ownerNode.Position.Y + 10f);
        }
    }
}
