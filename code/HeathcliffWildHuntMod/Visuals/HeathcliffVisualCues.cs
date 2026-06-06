using HeathcliffWildHuntMod.Visuals.Definition;

namespace HeathcliffWildHuntMod.Visuals;

/// <summary>
/// 希斯克利夫的 cue 集合。
/// <para>
/// 【已退役动画职责 / 资源待重建】角色动画已完全转 Spine 骨骼（见 <see cref="Heathcliff.GenerateAnimator"/>），
/// 原先这里维护的逐帧动画 cue（idle/hurt/cast/block/move/die + attack/attack2/attack3 及其 -d 形态）
/// 对应的贴图目录已随转 Spine 一并删除，相关路径全部失效，故清空为<b>空集</b>。
/// </para>
/// <para>
/// cue 基础设施（<see cref="ModVisualCues"/> DSL、<see cref="VisualCueSet"/>、<c>CueFrameSequencePlayer</c>）保留，
/// 计划改用于<b>特效播放</b>（剑气 / 变身光效 / buff 粒子等独立于角色 Spine 本体的叠加层）。
/// 届时在 <see cref="BuildDefault"/> / <see cref="BuildDullahan"/> 里按特效需要重新填充帧序列即可。
/// </para>
/// 注：当前没有活代码消费这些集合——<c>PatchNCreatureSetAnimationTrigger</c>（cue→动画的唯一入口）已停用。
/// </summary>
internal static class HeathcliffVisualCues
{
    /// <summary>资源根：将来特效 cue 从这里展开。</summary>
    private const string Root = "res://animations/characters/Heathcliff-WildHunt";

    /// <summary>默认形态 cue 集合（当前为空，待特效系统重建）。</summary>
    public static VisualCueSet Default { get; } = BuildDefault();

    /// <summary>杜拉罕形态 cue 集合（当前为空，待特效系统重建）。</summary>
    public static VisualCueSet Dullahan { get; } = BuildDullahan();

    // 当前两套集合均为空：逐帧动画 cue 已退役，特效 cue 尚未开始制作。
    // 重建特效时参考 git 历史里的旧实现（.Single(...)/.Sequence(...) 用法）往下面填即可。
    private static VisualCueSet BuildDefault() => ModVisualCues.CueSet().Build();

    private static VisualCueSet BuildDullahan() => ModVisualCues.CueSet().Build();
}
