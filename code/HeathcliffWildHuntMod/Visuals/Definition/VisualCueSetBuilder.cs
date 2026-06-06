using System;
using System.Collections.Generic;

namespace HeathcliffWildHuntMod.Visuals.Definition;

/// <summary>
/// 视觉 cue 集合的流式 builder。支持两种 cue：
/// <list type="bullet">
///   <item><description><see cref="Single"/> — 静态贴图 cue（idle/hit/dead 之类的单图状态）。</description></item>
///   <item><description><see cref="Sequence(string, VisualFrameSequence)"/> 或 <see cref="Sequence(string, Action{VisualFrameSequenceBuilder})"/> — 帧动画 cue。</description></item>
/// </list>
/// 同一个 cueKey 在两边只能存在其中一边，写入新条目会自动清掉旧的另一类，避免歧义。
/// </summary>
public sealed class VisualCueSetBuilder
{
    // 静态贴图字典（cueKey -> texPath）
    private readonly Dictionary<string, string> _textureByCue = new();
    // 帧序列字典（cueKey -> sequence）
    private readonly Dictionary<string, VisualFrameSequence> _sequenceByCue = new();

    private VisualCueSetBuilder() { }

    /// <summary>对外唯一入口：返回一个干净的新 builder 实例。</summary>
    public static VisualCueSetBuilder Create() => new();

    /// <summary>注册一个静态贴图 cue。若同 key 已有帧序列 cue，会被自动清掉。</summary>
    public VisualCueSetBuilder Single(string cueKey, string texturePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cueKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(texturePath);
        // 同 key 旧的帧序列必须先移除，防止运行时同时命中两路
        _sequenceByCue.Remove(cueKey);
        _textureByCue[cueKey] = texturePath;
        return this;
    }

    /// <summary>注册一个已构建好的帧序列 cue。若同 key 已有静态贴图 cue，会被自动清掉。</summary>
    public VisualCueSetBuilder Sequence(string cueKey, VisualFrameSequence sequence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cueKey);
        ArgumentNullException.ThrowIfNull(sequence);
        _textureByCue.Remove(cueKey);
        _sequenceByCue[cueKey] = sequence;
        return this;
    }

    /// <summary>用配置回调注册帧序列 cue：在 lambda 里链式调用 <c>seq.Frame(...).Frame(...)</c>。</summary>
    public VisualCueSetBuilder Sequence(string cueKey, Action<VisualFrameSequenceBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var seqBuilder = VisualFrameSequenceBuilder.Create();
        configure(seqBuilder);
        return Sequence(cueKey, seqBuilder.Build());
    }

    /// <summary>固化为不可变 <see cref="VisualCueSet"/>。空集合也允许（角色可能完全无视觉）。</summary>
    public VisualCueSet Build()
    {
        // 复制一份字典，避免后续 builder 引用泄漏导致外部修改
        return new VisualCueSet(
            TexturePathByCue: new Dictionary<string, string>(_textureByCue),
            FrameSequenceByCue: new Dictionary<string, VisualFrameSequence>(_sequenceByCue));
    }
}
