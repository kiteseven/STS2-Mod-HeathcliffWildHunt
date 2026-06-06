using System;
using System.Collections.Generic;
using Godot;

namespace HeathcliffWildHuntMod.Visuals.Definition;

/// <summary>
/// 帧序列流式 builder：链式调用 <see cref="Frame(string, float, Vector2?)"/> 添加帧，最后 <see cref="Build"/> 产出不可变 <see cref="VisualFrameSequence"/>。
/// 默认 Loop=false，调 <see cref="Loop"/> 切换为循环序列。
/// </summary>
public sealed class VisualFrameSequenceBuilder
{
    // 当前累积的帧列表（按调用顺序）
    private readonly List<VisualFrame> _frames = new();
    // 是否循环标记，默认 false
    private bool _loop;

    private VisualFrameSequenceBuilder() { }

    /// <summary>对外唯一入口：返回一个干净的新 builder 实例。</summary>
    public static VisualFrameSequenceBuilder Create() => new();

    /// <summary>追加一帧。<paramref name="durationSeconds"/> 必须为正数，否则视为立即切换。</summary>
    /// <param name="offset">
    /// 可选：该帧相对 Sprite2D 初始位置的偏移。null = 不偏移；常用于校正多帧 bbox 差异导致的角色漂移。
    /// </param>
    public VisualFrameSequenceBuilder Frame(string texturePath, float durationSeconds, Vector2? offset = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(texturePath);
        _frames.Add(new VisualFrame(texturePath, durationSeconds, offset));
        return this;
    }

    /// <summary>把整个序列标记为循环（默认 true，可显式传 false 取消）。</summary>
    public VisualFrameSequenceBuilder Loop(bool loop = true)
    {
        _loop = loop;
        return this;
    }

    /// <summary>固化为不可变序列。要求至少 1 帧。</summary>
    public VisualFrameSequence Build()
    {
        if (_frames.Count == 0)
        {
            // 没帧的序列没有播放意义，直接抛出，避免运行时静默不动
            throw new InvalidOperationException("VisualFrameSequence 至少要包含 1 帧。");
        }
        // 转成只读数组防止外部继续修改
        return new VisualFrameSequence(_frames.ToArray(), _loop);
    }
}
