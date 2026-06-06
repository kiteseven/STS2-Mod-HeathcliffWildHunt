using System;
using System.Linq;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using MegaCrit.Sts2.Core.Runs;

namespace HeathcliffWildHuntMod;

/// <summary>
///     商店角色显示希斯克利夫形象：用 vanilla ironclad 商店场景当「壳」（脚本/交互全正常），
///     在 _Ready 后把壳里第一个 ironclad SpineSprite **整节点替换**成 mod 骨骼自洽的 SpineSprite。
///     与 <see cref="PatchRestSiteCharacterSkeleton"/> 同一套方案、同一套根因（详见该类注释）：
///     <list type="bullet">
///     <item>不能直指 mod 自己的 merchant 场景：vanilla 根脚本在 mod pck 绑不上 → InvalidCast → 崩。</item>
///     <item>不能 SetSkeletonDataRes 热换：往已初始化的 ironclad SpineSprite 灌 wildhunt 骨骼 → atlas/material
///           不匹配 → native 崩。</item>
///     <item>正解：从能跑的战斗 visuals 场景取干净的 SpineSprite reparent 过来。</item>
///     </list>
///     ⚠️ vanilla NMerchantCharacter._Ready 用 <c>GetChild(0)</c> 取 SpineSprite 播 relaxed_loop，
///     故替换后必须保证新 SpineSprite 是**第一个子节点**（用 MoveChild 置顶）。
/// </summary>
[HarmonyPatch(typeof(NMerchantCharacter), "_Ready")]
internal static class PatchMerchantCharacterSkeleton
{
    /// <summary>mod 自己的战斗 visuals 场景：SpineSprite 骨骼已烤好、已验证可正常渲染。</summary>
    private const string CombatVisualsScenePath =
        "res://scenes/creature_visuals/heathcliff.tscn";

    /// <summary>战斗场景里 SpineSprite 子节点名（见 heathcliff.tscn，unique_name "Visuals"）。</summary>
    private const string VisualsSpineNodeName = "Visuals";

    /// <summary>商店待机动画（vanilla 播这个；wildhunt.skel 含 relaxed_loop）。</summary>
    private const string MerchantAnim = "relaxed_loop";

    private static void Prefix(NMerchantCharacter __instance)
    {
        // owner 门禁：仅当本局本地玩家是希斯克利夫才接管（其它角色 / 与其它 mod 共存时不动其商店形象）。
        // ⚠️ NMerchantCharacter 不像火堆那样自带 Player 引用；商店是单局单人为主，按本地玩家角色判定即可。
        if (LocalContext.GetMe(RunManager.Instance.DebugOnlyGetState())?.Character is not Heathcliff)
            return;

        try
        {
            // 1) 实例化 mod 战斗 visuals 场景，取出骨骼自洽的 SpineSprite
            var visualsScene = GD.Load<PackedScene>(CombatVisualsScenePath);
            if (visualsScene is null)
            {
                GD.PushWarning($"[WildHunt] 商店换形象失败：加载不到 {CombatVisualsScenePath}");
                return;
            }
            var visualsRoot = visualsScene.Instantiate();
            var newSpine = visualsRoot.GetNodeOrNull<Node2D>(VisualsSpineNodeName);
            if (newSpine is null || newSpine.GetClass() != "SpineSprite")
            {
                GD.PushWarning($"[WildHunt] 商店换形象失败：visuals 场景里找不到名为 '{VisualsSpineNodeName}' 的 SpineSprite。");
                visualsRoot.QueueFree();
                return;
            }
            visualsRoot.RemoveChild(newSpine);
            visualsRoot.QueueFree();

            // 2) 删掉壳里所有 ironclad SpineSprite，记录第一个的 transform 以便对位
            Vector2 pos = Vector2.Zero, scale = new Vector2(0.47f, 0.47f);
            bool gotTransform = false;
            foreach (Node2D child in __instance.GetChildren().OfType<Node2D>().ToList())
            {
                if (child.GetClass() != "SpineSprite")
                    continue;
                if (!gotTransform)
                {
                    pos = child.Position;
                    scale = child.Scale;
                    gotTransform = true;
                }
                __instance.RemoveChild(child);
                child.QueueFree();
            }

            // 3) 挂上新 SpineSprite，套用原 transform，并置为第一个子节点（vanilla GetChild(0) 依赖此）
            newSpine.Position = pos;
            newSpine.Scale = scale;
            __instance.AddChild(newSpine);
            __instance.MoveChild(newSpine, 0);

            // 4) 等骨骼就绪后播 relaxed_loop（vanilla 同款守护；缺动画则跳过不崩）
            var mega = new MegaSprite(newSpine);
            __instance.RunWhenSpineReady(mega, animState =>
            {
                if (mega.HasAnimation(MerchantAnim))
                    animState.SetAnimation(MerchantAnim, loop: true);
                else
                    GD.PushWarning($"[WildHunt] 商店骨骼缺动画 '{MerchantAnim}'，跳过设置。");
            });

            GD.Print("[WildHunt] 商店角色形象已替换为希斯克利夫（整节点替换 SpineSprite）。");
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[WildHunt] 商店换形象异常：{ex.Message}");
        }
    }
}
