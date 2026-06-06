using System.Collections.Generic;
using Godot;
using HarmonyLib;
using HeathcliffWildHuntMod.Combat.Ui;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace HeathcliffWildHuntMod;

/// <summary>
/// 能力图标角标：在 <see cref="NPower"/> 上为实现了 <see cref="IPowerCornerBadgeProvider"/> 的能力
/// 渲染额外的角落数字/文本（独立于 vanilla 主计数）。用于沉沦同时显示「强度 + 层数」等双数值场景。
///
/// 思路（自造轮子，不引用前置库）：
///   1) Postfix 接管 NPower.RefreshAmount——主计数每次刷新后，按 provider 返回的角标列表
///      创建/复用命名子 MegaLabel，定位到对应角落并复制主 label 的字体样式；
///   2) Postfix 接管 NPower._ExitTree——节点退出战斗树时清理角标，避免泄漏。
///
/// <para>
/// <b>结构说明（关键）：</b>本类 patch 两个<b>不同目标方法</b>，必须用「嵌套类」模式——
/// 每个嵌套类带类级 <c>[HarmonyPatch(目标)]</c> + 一个名为 <c>Postfix</c> 的方法。
/// 这是项目里 <c>PatchHeathcliffEnergyCounter</c>/<c>PatchHeathcliffMissingAssetPaths</c> 已验证可靠的写法。
/// 不能用「单类 + 方法级 [HarmonyPatch]」patch 多个目标——HarmonyLib 的 PatchClassProcessor
/// 对单类多目标处理不可靠，会导致整类 patch 失败、角标完全不显示。
/// </para>
/// </summary>
internal static class PatchNPowerCornerBadges
{
    /// <summary>角标子节点名字前缀，用于池化查找与退出清理。</summary>
    private const string BadgeNamePrefix = "WildHuntCornerBadge_";

    // NPower 的私有字段，靠反射读取（vanilla 未公开）。
    private static readonly AccessTools.FieldRef<NPower, PowerModel?> ModelField =
        AccessTools.FieldRefAccess<NPower, PowerModel?>("_model");
    private static readonly AccessTools.FieldRef<NPower, MegaLabel> AmountLabelField =
        AccessTools.FieldRefAccess<NPower, MegaLabel>("_amountLabel");

    // ── Power 图标角落定位矩形（单位为像素 Offset，以 NPower 左上为基准）──
    // 图标 40×40；vanilla 主计数占下排 -56..44 右对齐，数字落在图标右半/右缘（即「右」）。
    // 我们的角标用其余角带区；强度角标放下排左侧、右对齐贴图标左缘（即「左」）。
    private const float LabelLeft = -56f;
    private const float LabelRight = 44f;
    private const float LabelWidth = LabelRight - LabelLeft; // 整条带区宽度
    private const float TopRowTop = 0f;
    private const float TopRowBottom = 23f;
    private const float BottomRowTop = 21f;
    private const float BottomRowBottom = 44f;
    // 左下角标右边界：贴图标左缘（x≈0）稍留 2px 间隙，数字右对齐悬在图标左侧。
    private const float BottomLeftRight = -2f;

    /// <summary>主计数刷新后，按 provider 渲染角标。</summary>
    [HarmonyPatch(typeof(NPower), "RefreshAmount")]
    internal static class RefreshAmountPatch
    {
        private static void Postfix(NPower __instance)
        {
            var model = ModelField(__instance);
            var amountLabel = AmountLabelField(__instance);

            // ① 主计数文本改写：能力若实现 IPowerMainAmountTextProvider，则用其返回值覆盖
            //    vanilla 刚设好的 DisplayAmount 文本（如沉沦把「强度 层数」一起塞进主计数）。
            //    返回 null 表示沿用 vanilla 默认，不动。
            if (model is IPowerMainAmountTextProvider textProvider && amountLabel != null)
            {
                var mainText = textProvider.GetMainAmountText();
                if (mainText != null)
                    amountLabel.SetTextAutoSize(mainText);
            }

            // ② 角标渲染：能力未实现角标接口则清掉旧角标后返回。
            if (model is not IPowerCornerBadgeProvider provider)
            {
                HideAllBadges(__instance);
                return;
            }

            var badges = provider.GetPowerCornerBadges();
            GD.Print($"[WildHunt][Badge] {model.GetType().Name} 角标刷新：{badges.Count} 个，amountLabel={(amountLabel != null ? "有" : "null")}");

            int writeIndex = 0;
            var occupied = new HashSet<PowerBadgeCorner>();
            foreach (var badge in badges)
            {
                if (string.IsNullOrWhiteSpace(badge.Text)) continue;
                // 同一个角落只允许一个角标，重复的丢弃。
                if (!occupied.Add(badge.Corner)) continue;

                // 复制 vanilla 主计数 label 作为角标——连带继承字体/描边/阴影/MegaLabel 脚本，
                // 避免裸建 MegaLabel 因缺少 theme font override 触发 AssertThemeFontOverride 崩溃。
                var label = GetOrCreateBadgeLabel(__instance, writeIndex, amountLabel);
                ApplyCornerBounds(label, badge.Corner);
                label.AddThemeColorOverride(ThemeConstants.Label.FontColor, badge.Color ?? model.AmountLabelColor);
                label.SetTextAutoSize(badge.Text);
                label.Visible = true;
                writeIndex++;
            }

            HideBadgesFrom(__instance, writeIndex);
        }
    }

    /// <summary>退出战斗树时清理角标，避免泄漏。</summary>
    [HarmonyPatch(typeof(NPower), "_ExitTree")]
    internal static class ExitTreePatch
    {
        private static void Postfix(NPower __instance)
        {
            for (int i = 0; ; i++)
            {
                var node = __instance.GetNodeOrNull<MegaLabel>(BadgeNamePrefix + i);
                if (node == null) break;
                node.QueueFree();
            }
        }
    }

    /// <summary>
    /// 按索引获取或创建一个角标子 label（池化：按名字复用，避免重复 AddChild）。
    /// 新建时<b>复制</b>传入的主计数 label——继承其字体/字号/描边/阴影及 MegaLabel 脚本，
    /// 既保证视觉与原版一致，又避免裸建 MegaLabel 缺 theme font override 而崩溃。
    /// </summary>
    private static MegaLabel GetOrCreateBadgeLabel(NPower host, int index, MegaLabel? source)
    {
        var existing = host.GetNodeOrNull<MegaLabel>(BadgeNamePrefix + index);
        if (existing != null) return existing;

        // 复制主计数 label；source 为空时退化为裸建（极少发生，仅作兜底）。
        var label = source?.Duplicate((int)Node.DuplicateFlags.Scripts) as MegaLabel
                    ?? new MegaLabel();
        label.Name = BadgeNamePrefix + index;
        label.MouseFilter = Control.MouseFilterEnum.Ignore;
        label.ClipContents = false; // 不裁剪，避免大字号数字被截断
        host.AddChild(label);
        return label;
    }

    /// <summary>把角标锚定到 NPower 左上为基准的指定角落矩形。</summary>
    private static void ApplyCornerBounds(MegaLabel label, PowerBadgeCorner corner)
    {
        label.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        var (l, t, r, b) = corner switch
        {
            // 左上：左侧带区上排，文本左对齐
            PowerBadgeCorner.TopLeft => (0f, TopRowTop, LabelWidth, TopRowBottom),
            // 右上：右侧带区上排，文本右对齐
            PowerBadgeCorner.TopRight => (LabelLeft, TopRowTop, LabelRight, TopRowBottom),
            // 左下：从 label 左缘到图标左缘，下排，文本右对齐 → 数字悬在图标左侧（即「左」）
            PowerBadgeCorner.BottomLeft => (LabelLeft, BottomRowTop, BottomLeftRight, BottomRowBottom),
            _ => (0f, TopRowTop, LabelWidth, TopRowBottom),
        };
        label.OffsetLeft = l;
        label.OffsetTop = t;
        label.OffsetRight = r;
        label.OffsetBottom = b;

        label.HorizontalAlignment = corner switch
        {
            // 右上、左下都右对齐（左下右对齐让强度紧贴图标左缘）
            PowerBadgeCorner.TopRight => HorizontalAlignment.Right,
            PowerBadgeCorner.BottomLeft => HorizontalAlignment.Right,
            _ => HorizontalAlignment.Left,
        };
        label.VerticalAlignment = VerticalAlignment.Center;
    }

    private static void HideAllBadges(NPower host) => HideBadgesFrom(host, 0);

    /// <summary>隐藏从 startIndex 起的所有角标（本次刷新没用到的旧节点）。</summary>
    private static void HideBadgesFrom(NPower host, int startIndex)
    {
        for (int i = startIndex; ; i++)
        {
            var node = host.GetNodeOrNull<MegaLabel>(BadgeNamePrefix + i);
            if (node == null) break;
            node.Visible = false;
        }
    }
}
