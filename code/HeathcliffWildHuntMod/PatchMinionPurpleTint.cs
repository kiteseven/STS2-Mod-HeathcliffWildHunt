using Godot;
using HarmonyLib;
using HeathcliffWildHuntMod.Monsters;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace HeathcliffWildHuntMod;

/// <summary>
/// 狂猎随从的 Sprite2D 套一层紫色滤镜（modulate），使其外观统一为狂猎紫色调。
/// 在 NCreature._Ready 末尾触发——此时 Visuals 节点已挂载完毕，可以安全访问 Sprite2D。
/// </summary>
[HarmonyPatch(typeof(NCreature), nameof(NCreature._Ready))]
internal static class PatchMinionPurpleTint
{
    /// <summary>紫色滤镜颜色——狂猎主题深紫 #3A0B80（略调亮以保持贴图可见）。</summary>
    private static readonly Color PurpleTint = new Color(0.35f, 0.07f, 0.65f, 1.0f);

    private static void Postfix(NCreature __instance)
    {
        // 只处理狂猎随从——通过 Entity 上的 Monster 模型类型判断
        var monster = __instance.Entity?.Monster;
        if (monster is not WildHuntMinionModel) return;

        // 找到 Visuals 下的主 Sprite2D 并套 modulate
        var sprite = FindSprite(__instance.Visuals);
        if (sprite != null)
            sprite.Modulate = PurpleTint;
    }

    /// <summary>从 visuals 节点树中找出主 Sprite2D。</summary>
    private static Sprite2D? FindSprite(Node root)
    {
        // 优先按 unique name 查找 "%Visuals"
        var direct = root.GetNodeOrNull("%Visuals") ?? root.GetNodeOrNull("Visuals");
        if (direct is Sprite2D s) return s;

        if (root is Sprite2D rootSprite) return rootSprite;

        return SearchRecursive(root);
    }

    private static Sprite2D? SearchRecursive(Node parent)
    {
        foreach (var child in parent.GetChildren())
        {
            if (child is Sprite2D match) return match;
            var found = SearchRecursive(child);
            if (found != null) return found;
        }
        return null;
    }
}
