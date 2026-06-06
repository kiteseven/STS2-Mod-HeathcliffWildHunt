using System.Collections.Generic;
using Godot;

namespace HeathcliffWildHuntMod.Combat.Ui;

/// <summary>能力图标角标可用的角落。右下角是 vanilla 主计数专用，故不开放。</summary>
public enum PowerBadgeCorner
{
    /// <summary>左上角。</summary>
    TopLeft,
    /// <summary>右上角。</summary>
    TopRight,
    /// <summary>左下角。</summary>
    BottomLeft,
}

/// <summary>
/// 单个角标描述：放在哪个角、显示什么文本、可选字体颜色（为空则沿用主计数颜色）。
/// </summary>
public readonly record struct PowerCornerBadge(PowerBadgeCorner Corner, string Text, Color? Color = null);

/// <summary>
/// 由 <see cref="MegaCrit.Sts2.Core.Models.PowerModel"/> 子类实现，用于在能力图标
/// （<see cref="MegaCrit.Sts2.Core.Nodes.Combat.NPower"/>）的角落渲染额外数字/文本徽标，
/// 独立于 vanilla 的主计数 label。
/// 自造轮子，不依赖任何前置库——由 <c>PatchNPowerCornerBadges</c> 在主计数刷新后读取并渲染。
/// </summary>
public interface IPowerCornerBadgeProvider
{
    /// <summary>
    /// 返回要显示的角标列表。文本为空白的条目会被忽略；同一个角落只取第一个，重复角落的后续条目被丢弃。
    /// 列表为空表示当前不显示任何角标。
    /// </summary>
    IReadOnlyList<PowerCornerBadge> GetPowerCornerBadges();
}
