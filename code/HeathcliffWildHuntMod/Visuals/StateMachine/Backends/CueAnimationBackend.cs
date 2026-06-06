using System;
using System.Collections.Generic;
using Godot;
using HeathcliffWildHuntMod.Visuals.Definition;

namespace HeathcliffWildHuntMod.Visuals.StateMachine.Backends;

/// <summary>
/// 【未接通 / 已归档】早期状态机的 cue 后端，无活代码引用。动画现统一走 vanilla Spine；
/// cue 系统将转用于特效播放（届时可参考本类的帧序列/静态切图解析逻辑）。
/// <para>
/// 由 <see cref="VisualCueSet"/> 驱动的 <see cref="IAnimationBackend"/>：把 cue 键映射到帧序列或单张贴图，
/// 通过 <see cref="CueFrameSequencePlayer"/> 推进帧动画，或直接替换 <see cref="Sprite2D"/>.Texture 实现静态切图。
/// </para>
/// </summary>
/// <remarks>
/// 解析顺序：先查 <see cref="VisualCueSet.FrameSequenceByCue"/>，命中则走帧序列播放器；否则查
/// <see cref="VisualCueSet.TexturePathByCue"/> 切静态贴图。非循环静态 cue 在下一空闲帧（CreateTimer(0)）
/// 抛 <see cref="Completed"/>，避免在 <c>Play</c> 内同步重入状态机。
/// </remarks>
public sealed class CueAnimationBackend : IAnimationBackend
{
    // 整个角色一份 cue 集合（贴图 / 帧序列 / 样式）
    private readonly VisualCueSet _cues;
    // 帧序列 Finished 事件订阅句柄；连接 / 断开都用这同一个委托引用
    private readonly Action _onFinished;
    // 视觉根节点：CueFrameSequencePlayer 挂载点 & GetTree() 入口
    private readonly Node _root;
    // 真正承载贴图变化的 Sprite2D
    private readonly Sprite2D _sprite;

    // 当前正在播放的 cue id；null 代表 idle
    private string? _currentId;
    // 等待 Completed 后接力播的下一个 cue（Spine 队列语义的本地模拟）
    private string? _queuedId;
    // _queuedId 是否要循环
    private bool _queuedLoop;
    // 当前订阅了 Finished 的播放器；用于精确 Disconnect，避免泄漏
    private CueFrameSequencePlayer? _subscribedPlayer;

    /// <summary>把 cue 集合 <paramref name="cues"/> 绑定到挂在 <paramref name="root"/> 下的 <paramref name="sprite"/>。</summary>
    public CueAnimationBackend(Node root, Sprite2D sprite, VisualCueSet cues)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(sprite);
        ArgumentNullException.ThrowIfNull(cues);
        _root = root;
        _sprite = sprite;
        _cues = cues;
        // 一次性 alloc，重复 Subscribe/Unsubscribe 同一个委托引用
        _onFinished = OnSequenceFinished;
    }

    /// <inheritdoc />
    public Node? OwnerNode => _root;

    /// <inheritdoc />
    public event Action<string>? Started;

    /// <inheritdoc />
    public event Action<string>? Completed;

    /// <inheritdoc />
    public event Action<string>? Interrupted;

    /// <inheritdoc />
    public bool HasAnimation(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;

        // 帧序列优先：哪怕同名 cue 同时存在于 Texture 字典，也按帧序列优先（builder 应已互斥，但这里再防一次）
        if (_cues.FrameSequenceByCue is { Count: > 0 } sequences &&
            TryGetOrdinalIgnoreCase(sequences, id, out var sequence) &&
            sequence is { Frames.Count: > 0 })
            return true;

        return _cues.TexturePathByCue is { Count: > 0 } textures &&
               TryGetOrdinalIgnoreCase(textures, id, out var path) &&
               !string.IsNullOrWhiteSpace(path);
    }

    /// <inheritdoc />
    public void Play(string id, bool loop)
    {
        if (string.IsNullOrWhiteSpace(id)) return;

        // 替换前先把上一段标记为 Interrupted；若上一段是同 id 也不豁免——状态机自己会决定要不要重启
        if (_currentId != null) Interrupted?.Invoke(_currentId);

        UnsubscribeActivePlayer();
        // 暂停之前的帧序列推进，避免下面切静态图后还在被旧 sequence 覆盖
        CueFrameSequencePlayer.StopUnder(_root);

        _queuedId = null;
        _currentId = id;

        // ── 分支 1：帧序列 cue ──────────────────────────────
        if (_cues.FrameSequenceByCue is { Count: > 0 } sequences &&
            TryGetOrdinalIgnoreCase(sequences, id, out var sequence) &&
            sequence is { Frames.Count: > 0 })
        {
            var player = CueFrameSequencePlayer.EnsureUnder(_root);
            if (!player.TryStart(_sprite, sequence))
            {
                // 启动失败（极端：sprite 失效）——把 _currentId 留给下次 Play 覆盖即可
                return;
            }

            SubscribePlayer(player);
            Started?.Invoke(id);
            return;
        }

        // ── 分支 2：静态贴图 cue ────────────────────────────
        if (_cues.TexturePathByCue is not { Count: > 0 } textures ||
            !TryGetOrdinalIgnoreCase(textures, id, out var path) ||
            string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        // ResourceLoader 内部缓存，路径相同的贴图不会反复 IO
        var tex = ResourceLoader.Load<Texture2D>(path);
        if (tex == null) return;

        _sprite.Texture = tex;
        // 静态 cue 可选样式（缩放 / 翻转 / 调色）：仅 Sprite2D 维度，应用到 _sprite 上
        if (_cues.TextureStyleByCue is { Count: > 0 } styles &&
            TryGetOrdinalIgnoreCase(styles, id, out var style))
        {
            style.ApplyTo(_sprite);
        }

        Started?.Invoke(id);

        // 非循环静态 cue 没有"播完"概念，靠下一空闲帧抛 Completed 让状态机推进 NextState
        if (!loop) DeferCompletion(id);
    }

    /// <inheritdoc />
    public void Queue(string id, bool loop)
    {
        if (!HasAnimation(id)) return;

        // 当前没在播则等同于直接 Play，避免一直停在 idle
        if (_currentId == null)
        {
            Play(id, loop);
            return;
        }

        _queuedId = id;
        _queuedLoop = loop;
    }

    /// <inheritdoc />
    public void Stop()
    {
        // 静默停止：清状态 + 不抛事件（Interrupted/Completed 都不抛）
        _queuedId = null;
        _currentId = null;
        UnsubscribeActivePlayer();
        CueFrameSequencePlayer.StopUnder(_root);
    }

    /// <summary>主动释放：断开帧序列信号 + 停掉播放。可重复调用。</summary>
    public void Dispose()
    {
        UnsubscribeActivePlayer();
        CueFrameSequencePlayer.StopUnder(_root);
    }

    private void SubscribePlayer(CueFrameSequencePlayer player)
    {
        _subscribedPlayer = player;
        // 走 .NET event 而非 Godot Signal——本工程没接 Godot source generator
        player.Finished += _onFinished;
    }

    private void UnsubscribeActivePlayer()
    {
        if (_subscribedPlayer == null) return;

        // 节点可能已被父节点回收；只要委托引用相同，多次 -= 也是幂等的
        if (GodotObject.IsInstanceValid(_subscribedPlayer))
        {
            _subscribedPlayer.Finished -= _onFinished;
        }

        _subscribedPlayer = null;
    }

    private void OnSequenceFinished()
    {
        // 帧序列播完只会发一次 Finished；这里清状态 + 抛 Completed + 消费队列
        UnsubscribeActivePlayer();
        var id = _currentId ?? string.Empty;
        _currentId = null;
        Completed?.Invoke(id);
        ConsumeQueue();
    }

    /// <summary>静态 cue 的"伪 Completed"：通过 0 秒 SceneTreeTimer 推迟到下一空闲帧。</summary>
    private void DeferCompletion(string id)
    {
        if (!GodotObject.IsInstanceValid(_root)) return;

        var tree = _root.GetTree();
        if (tree == null)
        {
            // 没在场景树里——降级为同步抛事件（兜底，理论上不该发生）
            _currentId = null;
            Completed?.Invoke(id);
            ConsumeQueue();
            return;
        }

        var timer = tree.CreateTimer(0.0);
        timer.Timeout += () =>
        {
            // 期间被新 Play 覆盖了就不再抛旧 id 的 Completed
            if (_currentId != id) return;

            _currentId = null;
            Completed?.Invoke(id);
            ConsumeQueue();
        };
    }

    private void ConsumeQueue()
    {
        if (_queuedId is not { } next) return;

        var loop = _queuedLoop;
        _queuedId = null;
        // 注意 Play 内部会再次清 _queuedId，这里先取出本地变量避免被覆盖
        Play(next, loop);
    }

    /// <summary>大小写不敏感的字典查找：先精确（Ordinal）命中，未命中再线性扫描 OrdinalIgnoreCase。</summary>
    private static bool TryGetOrdinalIgnoreCase<TValue>(
        IReadOnlyDictionary<string, TValue> map, string key, out TValue? value)
    {
        if (map.TryGetValue(key, out var direct))
        {
            value = direct;
            return true;
        }

        foreach (var kv in map)
        {
            if (!string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase)) continue;
            value = kv.Value;
            return true;
        }

        value = default;
        return false;
    }
}
