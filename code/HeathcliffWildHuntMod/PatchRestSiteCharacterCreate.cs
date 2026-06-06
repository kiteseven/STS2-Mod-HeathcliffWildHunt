using HarmonyLib;
using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.RestSite;

namespace HeathcliffWildHuntMod;

/// <summary>
///     【已停用】火堆角色 Create 接管补绑脚本的尝试。
///     真正的火堆"没人"根因是 heathcliff_rest_site.tscn 引用 selection_reticle.tscn 时带了 mod 编辑器未注册的 uid
///     （uid://bgy88bk44xxvk），导出后子场景解析失败 → "Cannot get class ''" → 整个场景实例化崩溃。
///     已把场景的 ext_resource 改成 path-only（与能跑的 Ryoshu 火堆场景一致），原版 Create 即可正常实例化。
///
///     本补绑思路行不通且会引入新崩溃：SetScript 无法把已存在的 Node2D 托管实例原地变成 NRestSiteCharacter，
///     补绑后 as 仍为 null，再 QueueFree 已 dispose 的对象抛 ObjectDisposedException。
///     故去掉 [HarmonyPatch] 特性停用本类（WildHuntBootstrap 只 patch 带该特性的类），让原版 Create 接管。
///     保留文件作为该问题的记录，便于日后排查。
/// </summary>
// [HarmonyPatch(typeof(NRestSiteCharacter), nameof(NRestSiteCharacter.Create))] // 已停用：见上方说明
internal static class PatchRestSiteCharacterCreate
{
    /// <summary>游戏内 NRestSiteCharacter 脚本资源路径（带 uid 的可靠引用，用于补绑脚本类）。</summary>
    private const string ScriptPath = "res://src/Core/Nodes/RestSite/NRestSiteCharacter.cs";

    private static bool Prefix(Player player, int characterIndex, ref NRestSiteCharacter __result)
    {
        // 仅接管希斯克利夫；其它角色走原版
        if (player.Character is not Heathcliff)
            return true;

        // 取场景（原版同款缓存入口）
        var scene = PreloadManager.Cache.GetScene(player.Character.RestSiteAnimPath);
        if (scene is null)
        {
            GD.PushWarning("[WildHunt] 火堆场景加载失败，回退原版 Create。");
            return true; // 拿不到场景就放行原版（让原版自己报错，至少不掩盖问题）
        }

        // 非泛型实例化：先拿到原始节点，避免强转崩溃。用 try/catch 兜住实例化本身的异常
        // （如子场景缺失会抛 "Cannot get class ''"），失败则放行原版而非连带崩溃。
        Node node;
        try
        {
            node = scene.Instantiate(PackedScene.GenEditState.Disabled);
        }
        catch (System.Exception ex)
        {
            GD.PushWarning($"[WildHunt] 火堆场景实例化异常（可能子资源缺失）: {ex.Message}，回退原版 Create。");
            return true;
        }

        if (node is null)
        {
            GD.PushWarning("[WildHunt] 火堆场景实例化返回 null，回退原版 Create。");
            return true;
        }

        // 若根节点已是 NRestSiteCharacter（脚本绑定正常），直接用；否则补绑脚本类
        var character = node as NRestSiteCharacter;
        if (character is null)
        {
            // 脚本未绑定：手动加载 CSharpScript 资源并 SetScript，使节点变为正确类型。
            var script = GD.Load<Script>(ScriptPath);
            if (script is null)
            {
                GD.PushWarning($"[WildHunt] 无法加载火堆脚本资源 {ScriptPath}，回退原版 Create。");
                node.QueueFree();
                return true;
            }

            node.SetScript(script);
            // SetScript 后需要重新按托管类型获取实例：Godot 会为该节点重建 C# 包装对象
            character = node as NRestSiteCharacter;
            if (character is null)
            {
                GD.PushWarning("[WildHunt] 补绑脚本后仍非 NRestSiteCharacter，回退原版 Create。");
                node.QueueFree();
                return true;
            }
        }

        // 反射设置 Player（public get/private set）与 _characterIndex（private 字段），与原版 Create 等价
        Traverse.Create(character).Property("Player").SetValue(player);
        Traverse.Create(character).Field("_characterIndex").SetValue(characterIndex);

        __result = character;
        GD.Print("[WildHunt] 火堆角色 Create 已接管（补绑脚本类，绕过 InvalidCast）。");
        return false; // 跳过原版 Create
    }
}
