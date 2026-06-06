using System.Collections.Generic;

namespace HeathcliffWildHuntMod.Visuals.Definition;

/// <summary>
/// 整个角色的视觉 cue 集合：按 cueKey 把"静态贴图""帧动画序列""贴图样式"分别存放。
/// 同一个 cueKey 不会同时出现在 Texture / FrameSequence 两本字典里——builder 会自动清理冲突。
/// </summary>
/// <param name="TexturePathByCue">静态 cue：cueKey → 单张贴图路径（如 "idle_loop" → "res://.../idle.png"）。</param>
/// <param name="FrameSequenceByCue">帧序列 cue：cueKey → 帧序列。</param>
public sealed record VisualCueSet(
    IReadOnlyDictionary<string, string>? TexturePathByCue,
    IReadOnlyDictionary<string, VisualFrameSequence>? FrameSequenceByCue)
{
    /// <summary>静态贴图 cue 的可选样式覆盖（cueKey → style）：换贴图时一并应用样式（缩放/翻转等）。</summary>
    public IReadOnlyDictionary<string, VisualNodeStyle>? TextureStyleByCue { get; init; }
}
