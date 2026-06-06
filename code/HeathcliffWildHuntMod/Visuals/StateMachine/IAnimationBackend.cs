using System;
using Godot;

namespace HeathcliffWildHuntMod.Visuals.StateMachine;

/// <summary>
/// 【未接通 / 已归档】这套「后端无关动画状态机」是早期方案，从未被任何活代码使用。
/// 角色动画现已统一交给 vanilla Spine <c>CreatureAnimator</c>（见 <see cref="Heathcliff.GenerateAnimator"/>）。
/// 文件保留仅作参考，不参与编译期外的任何运行逻辑。
/// <para>
/// 与后端无关的动画驱动接口。<see cref="ModAnimStateMachine"/> 通过它驱动同一份状态图，
/// 后端可以是 Spine、Godot AnimationPlayer、AnimatedSprite2D，或本 mod 的 cue 帧序列播放器。
/// </para>
/// </summary>
/// <remarks>
/// 实现方在底层系统报告对应事件时，应触发 <see cref="Started"/> / <see cref="Completed"/> / <see cref="Interrupted"/>，
/// 这样状态机才能消费 <see cref="ModAnimState.NextState"/> 推进。
/// <see cref="Queue"/> 仅对 Spine 这种带真正队列语义的后端有意义；其他后端可转发为延迟 Play。
/// </remarks>
public interface IAnimationBackend
{
    /// <summary>持有该后端的 Godot 节点（视觉根 / 商人根等）；不适用时为 null。</summary>
    Node? OwnerNode { get; }

    /// <summary>后端开始播放某个动画 id 时触发。</summary>
    event Action<string>? Started;

    /// <summary>后端报告播放完成（一次循环结束 / 一次性播放结束）时触发。</summary>
    event Action<string>? Completed;

    /// <summary>后端报告播放被中断时触发。</summary>
    event Action<string>? Interrupted;

    /// <summary>查询后端是否能够播放给定动画 id。</summary>
    bool HasAnimation(string id);

    /// <summary>立即播放 <paramref name="id"/>，替换任何当前动画。</summary>
    /// <param name="id">动画 id；调用前应先用 <see cref="HasAnimation"/> 校验。</param>
    /// <param name="loop">循环提示；不支持循环的后端按"尽力而为"处理。</param>
    void Play(string id, bool loop);

    /// <summary>把 <paramref name="id"/> 排到当前动画之后。非队列后端可以延迟到 <see cref="Completed"/> 后再 Play。</summary>
    void Queue(string id, bool loop);

    /// <summary>静默停止当前播放（不触发 Interrupted/Completed），并清空待播放队列。默认空实现。</summary>
    void Stop() { }
}
