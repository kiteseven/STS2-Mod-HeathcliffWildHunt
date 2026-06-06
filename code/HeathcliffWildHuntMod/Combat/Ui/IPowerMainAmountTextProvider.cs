namespace HeathcliffWildHuntMod.Combat.Ui;

/// <summary>
/// 由 <see cref="MegaCrit.Sts2.Core.Models.PowerModel"/> 子类实现，用于<b>改写能力图标主计数 label 的文本</b>，
/// 替代 vanilla 默认的 <c>DisplayAmount.ToString()</c>。
/// <para>
/// 典型用途：单个能力需要在主计数位置同时显示两个数值（如沉沦的「强度 层数」），
/// 而不是拆到左下角标。返回 <c>null</c> 表示沿用 vanilla 默认文本，不做改写。
/// </para>
/// <para>
/// 自造轮子，不依赖任何前置库——由 <c>PatchNPowerCornerBadges</c> 在 vanilla 设完主计数后读取并覆写。
/// 与 <see cref="IPowerCornerBadgeProvider"/> 互相独立：一个能力可只实现其一，也可两者都实现。
/// </para>
/// </summary>
public interface IPowerMainAmountTextProvider
{
    /// <summary>
    /// 返回主计数 label 要显示的文本；<c>null</c> 表示用 vanilla 默认（<c>DisplayAmount</c>）。
    /// 文本对齐方式沿用 vanilla 主计数 label 自身的设置（通常右对齐，落在图标右侧）。
    /// </summary>
    string? GetMainAmountText();
}
