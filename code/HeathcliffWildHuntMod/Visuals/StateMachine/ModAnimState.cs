using System;
using System.Collections.Generic;
using System.Linq;

namespace HeathcliffWildHuntMod.Visuals.StateMachine;

/// <summary>
/// 【未接通 / 已归档】属于早期「后端无关动画状态机」实验，无活代码引用。动画现统一走 vanilla Spine。
/// <para>
/// 与后端无关的动画状态。语义上等价于 STS2 的 <c>AnimState</c>，但可被任何 <see cref="IAnimationBackend"/> 驱动。
/// </para>
/// </summary>
/// <remarks>
/// 状态转换规则：
/// <list type="bullet">
///   <item><description><see cref="NextState"/> 在当前动画完成 / 后端发出 Completed 时被消费；为 null 时停留不动。</description></item>
///   <item><description><see cref="CallTrigger"/> 解析通过 <see cref="AddBranch"/> 注册的分支，分支可带可选 guard。</description></item>
/// </list>
/// </remarks>
public sealed class ModAnimState
{
    // 触发器名 → 该触发器下的所有候选分支（按注册顺序）
    private readonly Dictionary<string, List<Branch>> _branches = new(StringComparer.Ordinal);

    /// <summary>用后端动画 id 创建状态。</summary>
    public ModAnimState(string id, bool isLooping = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        Id = id;
        IsLooping = isLooping;
    }

    /// <summary>后端动画 id（Spine 轨道名 / cue key / Godot 动画名）。</summary>
    public string Id { get; }

    /// <summary>当前状态是否循环。</summary>
    public bool IsLooping { get; }

    /// <summary>可选后续状态：当前完成时由状态机消费（如 attack→idle）。终止状态（die）保持 null。</summary>
    public ModAnimState? NextState { get; set; }

    /// <summary>可选 bounds 容器名，状态机会在进入 / 完成时通过 BoundsUpdated 转发。</summary>
    public string? BoundsContainer { get; init; }

    /// <summary>循环态完成至少一轮后置 true（用于 bounds / 调试逻辑）。</summary>
    public bool HasLooped { get; private set; }

    /// <summary>给当前状态注册一个条件分支：trigger 命中且 condition 通过时跳到 target。</summary>
    public void AddBranch(string trigger, ModAnimState target, Func<bool>? condition = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trigger);
        ArgumentNullException.ThrowIfNull(target);
        if (!_branches.TryGetValue(trigger, out var list))
        {
            list = new List<Branch>();
            _branches[trigger] = list;
        }
        list.Add(new Branch(target, condition));
    }

    /// <summary>解析 trigger 对应的第一个 guard 通过的目标状态；无可用分支时返回 null。</summary>
    public ModAnimState? CallTrigger(string trigger)
    {
        if (!_branches.TryGetValue(trigger, out var list)) return null;
        // 顺序找第一个 guard 通过的分支
        foreach (var b in list)
        {
            if (b.Condition == null || b.Condition()) return b.Target;
        }
        return null;
    }

    /// <summary>是否注册过给定 trigger 的至少一条分支。</summary>
    public bool HasTrigger(string trigger) => _branches.ContainsKey(trigger);

    /// <summary>标记本状态已完成至少一次循环。状态机内部调用，外部一般无需直接调。</summary>
    public void MarkHasLooped() => HasLooped = true;

    private readonly record struct Branch(ModAnimState Target, Func<bool>? Condition);
}
