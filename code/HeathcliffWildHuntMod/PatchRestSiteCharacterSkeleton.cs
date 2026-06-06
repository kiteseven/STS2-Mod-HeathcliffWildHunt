using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.RestSite;

namespace HeathcliffWildHuntMod;

/// <summary>
///     火堆角色显示希斯克利夫形象：用 vanilla ironclad 火堆场景当「壳」（脚本/reticle/hitbox 交互全正常），
///     把壳里那个 ironclad 的 SpineSprite **整节点替换**成 mod 自己骨骼已烤好的 SpineSprite。
///     <para>
///     为什么不能直接用 mod 自己的火堆场景：mod 场景里 reticle 子场景是 vanilla 资源（不在 mod pck），
///     嵌套实例化时解析不到 → "Cannot get class ''" → 根节点 NRestSiteCharacter 脚本绑不上
///     → InvalidCast(Node2D→NRestSiteCharacter) → 拿到裸节点，火堆没有任何操作选项。
///     </para>
///     <para>
///     为什么不能 SetSkeletonDataRes 热换骨骼：往**已初始化好 ironclad 骨骼**的 SpineSprite 灌 wildhunt
///     骨骼数据，atlas/material 仍是 ironclad 的 → 数据与渲染资源不匹配 → native Spine 渲染器崩溃。
///     </para>
///     <para>
///     ⚠ 关键时序根因（已用 minidump 定位）：首次进火堆必崩的真凶是 **vanilla _Ready 对「骨骼尚未就绪」
///     的 SpineSprite 调 <c>GetAnimationEnd()/SetTrackTime()</c>**。崩溃线程是 coreclr **GC 线程**
///     （RIP coreclr+0x247ac5，故障 +0x2e9302，其余线程全 park 在 ntdll）——这是 **native 堆被写坏后、
///     GC 扫堆时才触发**的 EXCEPTION_ACCESS_VIOLATION，能穿透 C# try/catch、且冷加载（骨骼初始化更慢）必现。
///     vanilla NRestSiteCharacter._Ready :137-141 会对每个子 SpineSprite 调
///     <c>new MegaSprite(child).GetAnimationState().SetAnimation(name)</c> 后再
///     <c>SetTrackTime(GetAnimationEnd()*随机)</c>；而我们 Prefix 刚 reparent 进来的新 SpineSprite
///     原生骨骼当帧尚未 init，<c>GetAnimationEnd()</c> 读到未初始化的原生 track 内存 → 写坏堆 → GC 崩。
///     （能跑的 <see cref="PatchMerchantCharacterSkeleton"/> 不崩，正是因为商店 _Ready 只 SetAnimation、
///     从不调 GetAnimationEnd/SetTrackTime。）
///     </para>
///     <para>
///     ✅ 解法（与前两次都不同的第三条路，规避两种死法）：
///     <list type="number">
///     <item><b>Prefix 只「删」不「加」</b>：删光壳里 ironclad SpineSprite 并记录 transform。此后
///           vanilla <c>GetChildSpineNodes()</c> 返回**空**，其 SetAnimation/SetTrackTime 循环执行 0 次，
///           **不碰任何 SpineSprite** → 既不会动旧节点（避免 Postfix 时代的悬空引用），也不会碰我们
///           尚未就绪的新节点（避免 Prefix 时代的 GetAnimationEnd 读坏堆）。</item>
///     <item><b>Postfix 才「加」并由我们自己设动画</b>：vanilla _Ready 跑完后，挂上希斯克利夫 SpineSprite，
///           用 <c>RunWhenSpineReady</c> **等骨骼真正就绪后**再 SetAnimation。设动画的主体从 vanilla 换成
///           我们自己、且严格延迟到就绪——彻底不存在「对未就绪骨骼调原生动画 API」的时机。</item>
///     </list>
///     </para>
/// </summary>
[HarmonyPatch(typeof(NRestSiteCharacter), "_Ready")]
internal static class PatchRestSiteCharacterSkeleton
{
    /// <summary>mod 自己的战斗 visuals 场景：SpineSprite 骨骼已烤好、已验证可正常渲染（战斗里就是它）。</summary>
    private const string CombatVisualsScenePath =
        "res://scenes/creature_visuals/heathcliff.tscn";

    /// <summary>战斗场景里 SpineSprite 子节点名（见 heathcliff.tscn，unique_name "Visuals"）。</summary>
    private const string VisualsSpineNodeName = "Visuals";

    /// <summary>
    ///     Prefix 删除旧 spine 时记录其 transform，供 Postfix 给新 spine 对位。
    ///     用 ConditionalWeakTable 以实例为键：多人模式可能同时存在多个火堆角色，且实例销毁后自动回收，不泄漏。
    /// </summary>
    private static readonly ConditionalWeakTable<NRestSiteCharacter, TransformBox> PendingTransforms = new();

    /// <summary>承载一对 (position, scale)，因 ConditionalWeakTable 值必须是引用类型。</summary>
    private sealed class TransformBox
    {
        public Vector2 Position;
        public Vector2 Scale = new(0.6f, 0.6f);
    }

    // ───────────────────────── Prefix：只删旧 ironclad SpineSprite，不加新节点 ─────────────────────────
    // 删空后 vanilla _Ready 的 GetChildSpineNodes() 为空 → 其 SetAnimation/SetTrackTime 循环不执行 →
    // vanilla 全程不碰任何 SpineSprite，既不会留悬空引用、也不会对未就绪骨骼调 GetAnimationEnd。
    private static void Prefix(NRestSiteCharacter __instance)
    {
        // 只接管希斯克利夫；其它角色保持 vanilla 火堆。
        if (__instance.Player?.Character is not Heathcliff)
            return;

        var box = new TransformBox();
        bool gotTransform = false;

        try
        {
            // 删光壳里所有 ironclad SpineSprite，记录第一个的 transform 以便新节点对位。
            foreach (Node2D child in __instance.GetChildren().OfType<Node2D>().ToList())
            {
                if (child.GetClass() != "SpineSprite")
                    continue;
                if (!gotTransform)
                {
                    box.Position = child.Position;
                    box.Scale = child.Scale;
                    gotTransform = true;
                }
                __instance.RemoveChild(child);
                child.QueueFree();
            }

            // 记录 transform，交给 Postfix 用（即便没删到 spine 也存默认值，保证 Postfix 必能取到）。
            PendingTransforms.Remove(__instance);
            PendingTransforms.Add(__instance, box);
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[WildHunt] 火堆移除旧形象异常：{ex.Message}");
        }
    }

    // ───────────────────────── Postfix：加希斯克利夫 SpineSprite，并由我们自己延迟设动画 ─────────────────────────
    // vanilla _Ready 已跑完（且因 Prefix 删空而没碰任何 spine），此时挂新节点安全；
    // 动画交给 RunWhenSpineReady 等骨骼就绪后再设，绝不在未就绪时调原生动画 API。
    private static void Postfix(NRestSiteCharacter __instance)
    {
        if (__instance.Player?.Character is not Heathcliff)
            return;

        // 取回 Prefix 记录的 transform（取不到则用默认）。
        if (!PendingTransforms.TryGetValue(__instance, out var box))
            box = new TransformBox();
        PendingTransforms.Remove(__instance);

        // 本章节对应的 loop 动画（与 vanilla _Ready 的 switch 一致；wildhunt.skel 三个 loop 都在）。
        string animName = __instance.Player.RunState.CurrentActIndex switch
        {
            0 => "overgrowth_loop",
            1 => "hive_loop",
            2 => "glory_loop",
            _ => "overgrowth_loop", // 越界兜底，避免 vanilla 那样直接抛异常
        };

        try
        {
            // 1) 实例化 mod 自己的战斗 visuals 场景，取出骨骼自洽的 SpineSprite。
            var visualsScene = GD.Load<PackedScene>(CombatVisualsScenePath);
            if (visualsScene is null)
            {
                GD.PushWarning($"[WildHunt] 火堆换形象失败：加载不到 {CombatVisualsScenePath}");
                return;
            }
            var visualsRoot = visualsScene.Instantiate();
            var newSpine = visualsRoot.GetNodeOrNull<Node2D>(VisualsSpineNodeName);
            if (newSpine is null || newSpine.GetClass() != "SpineSprite")
            {
                GD.PushWarning($"[WildHunt] 火堆换形象失败：visuals 场景里找不到名为 '{VisualsSpineNodeName}' 的 SpineSprite。");
                visualsRoot.QueueFree();
                return;
            }
            // 从其原父节点摘下（visualsRoot 未进树，合法），其余部分丢弃。
            visualsRoot.RemoveChild(newSpine);
            visualsRoot.QueueFree();

            // 2) 挂到壳上并置顶，套用旧 ironclad spine 的 transform 对位。
            newSpine.Position = box.Position;
            newSpine.Scale = box.Scale;
            __instance.AddChild(newSpine);
            __instance.MoveChild(newSpine, 0);

            // 3) 等骨骼**真正就绪**后才设动画（设动画的主体是我们自己，且严格延迟——
            //    彻底规避「对未就绪骨骼调 GetAnimationEnd/SetAnimation」这类写坏原生堆的时机）。
            var mega = new MegaSprite(newSpine);
            __instance.RunWhenSpineReady(mega, animState =>
            {
                if (mega.HasAnimation(animName))
                    animState.SetAnimation(animName, loop: true);
                else
                    GD.PushWarning($"[WildHunt] 火堆骨骼缺动画 '{animName}'，跳过设置。");
            });

            GD.Print($"[WildHunt] 火堆角色形象已替换为希斯克利夫（Prefix 删旧 + Postfix 加新并延迟设动画 {animName}）。");
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[WildHunt] 火堆换形象异常：{ex.Message}");
        }
    }
}
