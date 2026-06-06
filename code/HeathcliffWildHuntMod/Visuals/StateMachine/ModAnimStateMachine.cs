using System;
using MegaCrit.Sts2.Core.Logging;

namespace HeathcliffWildHuntMod.Visuals.StateMachine;

/// <summary>
/// 【未接通 / 已归档】早期「后端无关动画状态机」实验，无活代码引用。动画现统一走 vanilla <c>CreatureAnimator</c>。
/// <para>
/// 与后端无关的动画状态机。语义对齐 STS2 的 <c>CreatureAnimator</c>：
/// SetTrigger 先看 any-state 分支，再看当前状态分支；NextState 在进入时入队、完成时消费。
/// </para>
/// </summary>
/// <remarks>
/// 终止态（如 die）通过把 NextState 留为 null 表示，完成后保持当前状态不再推进。
/// </remarks>
public sealed class ModAnimStateMachine
{
    // 合成的 any-state，承载跨状态触发器（hit/cast/attack 等可在任何状态下命中）
    private readonly ModAnimState _anyState = new("__anyState");
    private bool _disposed;

    /// <summary>包装 backend 并订阅其事件。</summary>
    public ModAnimStateMachine(IAnimationBackend backend)
    {
        ArgumentNullException.ThrowIfNull(backend);
        Backend = backend;
        Backend.Started += OnBackendStarted;
        Backend.Completed += OnBackendCompleted;
        Backend.Interrupted += OnBackendInterrupted;
    }

    /// <summary>当前激活状态；Start 之前 / Dispose 之后为 null。</summary>
    public ModAnimState? Current { get; private set; }

    /// <summary>底层动画后端。组合场景下需要直接访问时可用。</summary>
    public IAnimationBackend Backend { get; }

    /// <summary>状态进入 / 完成 / 中断时，若状态有 BoundsContainer 则转发该字符串。</summary>
    public event Action<string>? BoundsUpdated;

    /// <summary>当后端开始播放当前状态对应动画时触发。</summary>
    public event Action<ModAnimState>? AnimationStarted;

    /// <summary>当后端报告当前状态完成时触发（循环结束或一次性结束）。</summary>
    public event Action<ModAnimState>? AnimationCompleted;

    /// <summary>当后端报告当前状态被中断时触发。</summary>
    public event Action<ModAnimState>? AnimationInterrupted;

    /// <summary>注册 any-state 分支：在任何当前状态下命中 trigger 都能转移。</summary>
    public void AddAnyState(string trigger, ModAnimState state, Func<bool>? condition = null)
        => _anyState.AddBranch(trigger, state, condition);

    /// <summary>进入初始状态，触发后端播放并发 BoundsUpdated。</summary>
    public void Start(ModAnimState initial)
    {
        ArgumentNullException.ThrowIfNull(initial);
        if (_disposed) return;
        EnterState(initial);
    }

    /// <summary>是否注册过给定 any-state trigger。</summary>
    public bool HasTrigger(string trigger) => _anyState.HasTrigger(trigger);

    /// <summary>触发跳转：先查 any-state，再查当前状态；命中则进入目标状态。</summary>
    public void SetTrigger(string trigger)
    {
        if (_disposed || string.IsNullOrWhiteSpace(trigger)) return;
        // any-state 优先级高于当前状态——这与 STS2 vanilla CreatureAnimator 一致
        var target = _anyState.CallTrigger(trigger) ?? Current?.CallTrigger(trigger);
        if (target == null) return;
        EnterState(target);
    }

    /// <summary>解绑后端事件，可重复调用。</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Backend.Started -= OnBackendStarted;
        Backend.Completed -= OnBackendCompleted;
        Backend.Interrupted -= OnBackendInterrupted;
        Current = null;
    }

    /// <summary>真正进入某个状态：让后端播该状态对应动画，并把 NextState 链入队。</summary>
    private void EnterState(ModAnimState state)
    {
        if (!Backend.HasAnimation(state.Id))
        {
            // 后端没有这个动画 id 时只警告不抛——避免角色卡死，相当于这条路径被静默跳过
            Log.Warn($"[WildHunt-Anim] backend missing animation id '{state.Id}' (owner={Backend.OwnerNode?.Name})");
            return;
        }

        Current = state;
        Backend.Play(state.Id, state.IsLooping);

        if (state.BoundsContainer != null) BoundsUpdated?.Invoke(state.BoundsContainer);
        if (state.NextState != null) QueueChain(state.NextState);
    }

    /// <summary>把 NextState 链顺次 Queue 给后端（Spine 等支持队列的后端能直接受益）。</summary>
    private void QueueChain(ModAnimState state)
    {
        while (true)
        {
            if (!Backend.HasAnimation(state.Id)) return;
            Backend.Queue(state.Id, state.IsLooping);
            if (state.NextState != null) { state = state.NextState; continue; }
            break;
        }
    }

    private void OnBackendStarted(string _)
    {
        if (Current is not { } state) return;
        // 首次 Start 时若有 bounds 容器则更新一次；后续循环不再重复更新
        if (state is { HasLooped: false, BoundsContainer: not null })
            BoundsUpdated?.Invoke(state.BoundsContainer);
        AnimationStarted?.Invoke(state);
    }

    private void OnBackendCompleted(string _)
    {
        if (Current is not { } state) return;
        if (state is { HasLooped: false, BoundsContainer: not null })
            BoundsUpdated?.Invoke(state.BoundsContainer);
        if (state is { IsLooping: true, HasLooped: false }) state.MarkHasLooped();

        AnimationCompleted?.Invoke(state);
        // 防御：回调可能已经把 Current 切走了
        if (Current != state) return;
        if (state.NextState != null) Current = state.NextState;
    }

    private void OnBackendInterrupted(string _)
    {
        if (Current is not { } state) return;
        if (state.BoundsContainer != null) BoundsUpdated?.Invoke(state.BoundsContainer);
        AnimationInterrupted?.Invoke(state);
    }
}
