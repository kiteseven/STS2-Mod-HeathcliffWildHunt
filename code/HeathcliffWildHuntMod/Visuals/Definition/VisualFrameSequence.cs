using System.Collections.Generic;

namespace HeathcliffWildHuntMod.Visuals.Definition;

/// <summary>
/// 帧动画序列：一组 <see cref="VisualFrame"/> + 是否循环。
/// 不可变 record，便于跨帧/跨实例共享同一份序列定义。
/// </summary>
/// <param name="Frames">按播放顺序排列的帧列表，至少 1 帧。</param>
/// <param name="Loop">true 表示无限循环（idle 类）；false 表示播完一次后触发 Finished（attack/hit 类）。</param>
public sealed record VisualFrameSequence(IReadOnlyList<VisualFrame> Frames, bool Loop = false);
