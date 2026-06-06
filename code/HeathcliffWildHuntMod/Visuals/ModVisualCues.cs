using HeathcliffWildHuntMod.Visuals.Definition;

namespace HeathcliffWildHuntMod.Visuals;

/// <summary>
/// 视觉 cue 静态门面：所有 mod 在角色 <c>AssetProfile</c> 里使用的统一入口。
/// 用法：<c>ModVisualCues.CueSet().Single(...).Sequence(...).Build()</c>。
/// </summary>
public static class ModVisualCues
{
    /// <summary>开一个新的 cue 集合 builder。</summary>
    public static VisualCueSetBuilder CueSet() => VisualCueSetBuilder.Create();

    /// <summary>开一个新的帧序列 builder（独立使用，便于复用同一段序列绑到多个 cue）。</summary>
    public static VisualFrameSequenceBuilder FrameSequence() => VisualFrameSequenceBuilder.Create();
}
