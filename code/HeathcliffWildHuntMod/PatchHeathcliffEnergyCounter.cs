using Godot;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace HeathcliffWildHuntMod;

/// <summary>
/// 狂猎能量表盘——纯 C# 代码构造，只显示 Layer1 大发光 orb 层。<br/>
/// 不加载任何 TSCN 场景文件，不引用任何 ironclad 资源，不引用基类 C# 脚本。
/// </summary>
internal static class PatchHeathcliffEnergyCounter
{
    /// <summary>基类 getter 计算出的路径，仅调试验证用。</summary>
    [HarmonyPatch(typeof(CharacterModel), "get_EnergyCounterPath")]
    internal static class EnergyCounterPath_Verify
    {
        private static void Postfix(CharacterModel __instance, ref string __result)
        {
            if (__instance is Heathcliff)
                GD.Print($"[WildHunt] EnergyCounterPath = {__result}");
        }
    }

    /// <summary>
    /// 狂猎专属：new NEnergyCounter + 只建 Layer1 静态发光层 + 空 RotationLayers 防止 NRE。
    /// 不创建 VFX 节点——_Ready 里 GetNode 找不到会返回 null，?.Restart() 安全跳过。
    /// </summary>
    [HarmonyPatch(typeof(NEnergyCounter), nameof(NEnergyCounter.Create))]
    internal static class Create_Heathcliff
    {
        static bool Prefix(Player player, ref NEnergyCounter __result)
        {
            if (player.Character is not Heathcliff) return true;

            GD.Print("[WildHunt] 构造狂猎专属能量表盘（纯代码，仅 Layer1）...");

            var counter = new NEnergyCounter
            {
                Name = "HeathcliffEnergyCounter",
                OffsetRight = 128,
                OffsetBottom = 128,
                PivotOffset = new Vector2(64, 64)
            };

            // ── Layers ──
            var layers = new Control
            {
                Name = "Layers",
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            layers.SetAnchorsPreset(Control.LayoutPreset.FullRect);

            // Layer1 — 狂猎大发光 orb（唯一可见层）
            var layer1 = new TextureRect
            {
                Name = "Layer1",
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            layer1.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            var tex1 = GD.Load<Texture2D>("res://images/ui/combat/energy_counters/heathcliff/heathcliff_orb_layer_1.png");
            if (tex1 != null) layer1.Texture = tex1;
            layers.AddChild(layer1);

            // RotationLayers — 空容器，仅防止 _Process / RefreshLabel 内部的 NRE
            var rotationLayers = new Control
            {
                Name = "RotationLayers",
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            rotationLayers.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            layers.AddChild(rotationLayers);

            // ── Label（MegaLabel，带字体覆写避免 AssertThemeFontOverride 异常） ──
            var label = new MegaLabel
            {
                Name = "Label",
                Text = "3/3",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MinFontSize = 32,
                MaxFontSize = 36
            };
            label.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            label.OffsetLeft = 16;
            label.OffsetTop = -29;
            label.OffsetRight = -16;
            label.OffsetBottom = 29;
            label.AddThemeColorOverride("font_color", new Color(1, 0.9647059f, 0.8862745f, 1));
            label.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.1882353f));
            label.AddThemeColorOverride("font_outline_color", new Color(0.227f, 0.043f, 0.502f, 1));
            label.AddThemeConstantOverride("shadow_offset_x", 3);
            label.AddThemeConstantOverride("shadow_offset_y", 2);
            label.AddThemeConstantOverride("outline_size", 16);
            label.AddThemeConstantOverride("shadow_outline_size", 16);
            label.AddThemeFontSizeOverride("font_size", 36);

            // MegaLabel 强制要求字体——加载基类游戏里的 Kreon Bold
            var font = GD.Load<Font>("res://themes/kreon_bold_shared.tres");
            if (font != null)
                label.AddThemeFontOverride("font", font);

            // ── 组装节点树 ──
            counter.AddChild(layers);
            counter.AddChild(label);

            // 设置 owner 供 % 前缀 GetNode 查找
            layers.Owner = counter;
            layer1.Owner = counter;
            rotationLayers.Owner = counter;
            label.Owner = counter;

            MarkUnique(layers);
            MarkUnique(rotationLayers);

            // 注入 _player 私有字段
            Traverse.Create(counter).Field("_player").SetValue(player);

            __result = counter;
            return false;
        }

        /// <summary>设置 Godot scene-unique 标志，使 %NodeName 查找能定位到该节点。</summary>
        private static void MarkUnique(Node node)
        {
            node.Set("unique_name_in_owner", true);
        }
    }
}
