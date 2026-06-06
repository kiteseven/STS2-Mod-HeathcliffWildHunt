using HarmonyLib;
using MegaCrit.Sts2.Core.Models;

namespace HeathcliffWildHuntMod;

/// <summary>
/// 把 <see cref="Heathcliff"/> 在 <see cref="CharacterModel.AssetPaths"/> /
/// <see cref="CharacterModel.AssetPathsCharacterSelect"/> 里通过 <c>Id.Entry.ToLowerInvariant()</c>
/// 拼出来的全部派生资源路径重定向到 ironclad 现成资源，避免 PreloadManager 加载失败。
/// <para>
/// 背景：原版 <c>CharacterModel</c> 一共 9 条派生路径（VisualsPath、IconTexturePath、IconPath、
/// EnergyCounterPath、RestSiteAnimPath、MerchantAnimPath、CharacterSelectTransitionPath、
/// MapMarkerPath、TrailPath）。其中 IconTexturePath 和 CharacterSelectTransitionPath 已分别
/// 通过用户提供的 <c>character_icon_heathcliff.png</c> 与
/// <see cref="PatchHeathcliffCharacterSelectTransitionPath"/> 解决。
/// </para>
/// <para>
/// 关键：<c>MapMarkerPath</c> 缺失会让 <c>MapMarker</c> 返回 null，进而
/// <c>NMapScreen.SetMap</c> 抛 NullReferenceException —— 这是 embark 后地图建不起来 / 黑屏的真凶
/// （godot.log:943 NullReferenceException at NMapScreen.SetMap）。
/// </para>
/// <para>
/// 设计案：每个 getter Postfix 一次，仅当 instance 是 <see cref="Heathcliff"/> 才覆盖 __result，
/// 其它角色保持原样。后续若做了 Heathcliff 专属资产，把对应常量替换成 mod pck 内真实路径即可。
/// </para>
/// </summary>
internal static class PatchHeathcliffMissingAssetPaths
{
    // 注意：VisualsPath 不再 patch —— 已在 res://scenes/creature_visuals/heathcliff.tscn 提供本 mod 自己的 Spine
    //       场景（SpineSprite + wildhunt 骨骼），HasSpineAnimation==true 让 vanilla CreatureAnimator 接管动画播放
    //       （见 Heathcliff.GenerateAnimator）。逐帧 cue 系统因 HasSpineAnimation 短路而自动停用，保留作历史/回退。
    // 其它派生路径仍统一指向 ironclad 现成资源，确保 PreloadManager 能拿到 Resource 实例。
    // 注：IconPath 已换成本 mod 自己的 heathcliff_icon.tscn
    private const string HeathcliffIconScene =
        "res://scenes/ui/character_icons/heathcliff_icon.tscn";
    // 火堆场景：用 vanilla ironclad 火堆场景。
    // 这是目前唯一能让根节点正确绑上 NRestSiteCharacter 脚本、reticle/hitbox 交互正常的选择
    // （vanilla 资源的脚本与 selection_reticle.tscn 不在 mod pck 内，mod 自己的场景按 res:// 路径
    //  引用它们时导出后悬空 → "Cannot get class ''" + InvalidCast(Node2D→NRestSiteCharacter)
    //  → 拿到裸 Node2D → 没有任何火堆操作选项）。
    // ⚠️ 形象（骨骼）暂时是铁甲战士：之前用 PatchRestSiteCharacterSkeleton 运行时 SetSkeletonDataRes
    //    把骨骼热换成希斯克利夫会 native 崩（往 ironclad SpineSprite 灌 wildhunt 骨骼，atlas/material
    //    不匹配，Spine 渲染器直接崩，任何时序守护都救不了），故该 patch 已停用。
    //    换形象的正解需另想办法（整节点替换 / 让 mod 场景脚本可解析），作为后续打磨项。
    private const string IroncladRestSiteScene =
        "res://scenes/rest_site/characters/ironclad_rest_site.tscn";
    // 商店场景：用 vanilla ironclad 商店场景当「壳」（脚本/交互正常），形象由
    // PatchMerchantCharacterSkeleton 在 _Ready 后整节点替换成希斯克利夫（与火堆同方案）。
    // ⚠️ 不能直指 mod 自己的 heathcliff_merchant.tscn：其根脚本 NMerchantCharacter.cs 是 vanilla 资源、
    //    不在 mod pck → 导出后绑不上 → InvalidCast(Node2D→NMerchantCharacter) → 进商店崩溃。
    private const string IroncladMerchantScene =
        "res://scenes/merchant/characters/ironclad_merchant.tscn";
    private const string IroncladMapMarker =
        "res://images/packed/map/icons/map_marker_ironclad.png";
    private const string IroncladCardTrailScene =
        "res://scenes/vfx/card_trail_ironclad.tscn";

    // 不再 patch get_VisualsPath：让 vanilla getter 返回默认路径
    // res://scenes/creature_visuals/heathcliff.tscn，PreloadManager 会从 mod pck 里加载本 mod 自己提供的 Spine 场景。

    /// <summary>character_icons 场景：玩家选人后右上角小头像，protected virtual 的 IconPath。</summary>
    [HarmonyPatch(typeof(CharacterModel), "get_IconPath")]
    internal static class IconPathPatch
    {
        private static void Postfix(CharacterModel __instance, ref string __result)
        {
            if (__instance is not Heathcliff) return;
            __result = HeathcliffIconScene;
        }
    }

    /// <summary>rest_site/characters 场景：营火界面的角色动画。</summary>
    [HarmonyPatch(typeof(CharacterModel), "get_RestSiteAnimPath")]
    internal static class RestSiteAnimPathPatch
    {
        private static void Postfix(CharacterModel __instance, ref string __result)
        {
            if (__instance is not Heathcliff) return;
            __result = IroncladRestSiteScene;
        }
    }

    /// <summary>merchant/characters 场景：商店界面的角色动画。</summary>
    [HarmonyPatch(typeof(CharacterModel), "get_MerchantAnimPath")]
    internal static class MerchantAnimPathPatch
    {
        private static void Postfix(CharacterModel __instance, ref string __result)
        {
            if (__instance is not Heathcliff) return;
            __result = IroncladMerchantScene;
        }
    }

    /// <summary>map_marker_*.png：地图上代表玩家的小图标，<b>缺失会引发 NMapScreen 黑屏</b>。protected virtual。</summary>
    [HarmonyPatch(typeof(CharacterModel), "get_MapMarkerPath")]
    internal static class MapMarkerPathPatch
    {
        private static void Postfix(CharacterModel __instance, ref string __result)
        {
            if (__instance is not Heathcliff) return;
            __result = IroncladMapMarker;
        }
    }

    /// <summary>card_trail_*.tscn：出牌时的拖尾 VFX 场景。</summary>
    [HarmonyPatch(typeof(CharacterModel), "get_TrailPath")]
    internal static class TrailPathPatch
    {
        private static void Postfix(CharacterModel __instance, ref string __result)
        {
            if (__instance is not Heathcliff) return;
            __result = IroncladCardTrailScene;
        }
    }
}
