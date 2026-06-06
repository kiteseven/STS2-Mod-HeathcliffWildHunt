using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace HeathcliffWildHuntMod;

/// <summary>
/// 狂猎希斯克利夫的卡牌边框 / 能量图标 / banner / 类型牌统一染成狂猎主题紫色。
/// 在 NCard._Ready 末尾触发——此时所有卡牌子节点已通过 Reload() 挂载完毕。
/// </summary>
[HarmonyPatch(typeof(NCard), nameof(NCard._Ready))]
internal static class PatchCardFrameColors
{
    /// <summary>卡牌外框紫色 SelfModulate——基于狂猎主题深紫 #3A0B80，略提亮以确保卡框图案可见。</summary>
    private static readonly Color FramePurpleTint = new Color(0.35f, 0.08f, 0.65f, 1.0f);

    /// <summary>能量图标紫色 SelfModulate（比外框稍亮，确保能耗数字清晰可辨）。</summary>
    private static readonly Color EnergyIconPurpleTint = new Color(0.40f, 0.10f, 0.70f, 1.0f);

    /// <summary>Banner 色调偏移——狂猎深紫 #3A0B80 对应 HSV hue ≈ 266°/360°。</summary>
    private const float BannerPurpleHue = 0.74f;

    /// <summary>是否已对共享 banner 材质做过色相偏移（只需做一次，避免重复设置）。</summary>
    private static bool _bannerHueAlreadyShifted;

    // Godot shader 参数名（与 vanilla banner/enchantment 材质一致）
    private static readonly StringName ShaderParamH = "_h";

    /// <summary>
    /// NCard._Ready 末尾回调：检查是否为狂猎卡牌，是则对卡框 / 能量图标套紫色 SelfModulate，
    /// 并首次碰到共享 banner 材质时把色相参数扭到紫色。
    /// </summary>
    private static void Postfix(NCard __instance)
    {
        // 仅处理狂猎卡池的卡牌
        if (__instance.Model?.Pool is not HeathcliffCardPool) return;

        // —— 卡牌外框 ——
        var frame = __instance.GetNodeOrNull<TextureRect>("%Frame");
        if (frame != null)
            frame.SelfModulate = FramePurpleTint;

        // —— 能量图标（可打出） ——
        var energyIcon = __instance.GetNodeOrNull<TextureRect>("%EnergyIcon");
        if (energyIcon != null)
            energyIcon.SelfModulate = EnergyIconPurpleTint;

        // —— 能量图标（不可打出时的红色叉号） ——
        var unplayableEnergyIcon = __instance.GetNodeOrNull<TextureRect>("%UnplayableEnergyIcon");
        if (unplayableEnergyIcon != null)
            unplayableEnergyIcon.SelfModulate = EnergyIconPurpleTint;

        // —— 共享 Banner 材质：只需全局修改一次色相参数 ——
        if (!_bannerHueAlreadyShifted)
        {
            var banner = __instance.GetNodeOrNull<TextureRect>("%TitleBanner");
            if (banner?.Material is ShaderMaterial bannerMat)
            {
                bannerMat.SetShaderParameter(ShaderParamH, BannerPurpleHue);
                _bannerHueAlreadyShifted = true; // 共享材质，改一次全部卡牌生效
            }
        }
    }
}
