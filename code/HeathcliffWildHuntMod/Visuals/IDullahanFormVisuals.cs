using HeathcliffWildHuntMod.Visuals.Definition;

namespace HeathcliffWildHuntMod.Visuals;

/// <summary>
/// 杜拉罕形态视觉扩展接口：角色实现后，<see cref="ModCreatureVisualPlayback"/> 会在玩家身上 DullahanPower 层数 &gt; 0 时
/// 改用本接口返回的 cue 集合，从而切到 "-d" 形态贴图。
/// 与 <see cref="IModCharacterVisualCues"/> 解耦：不需要变形的角色可以只实现前者。
/// </summary>
public interface IDullahanFormVisuals
{
    /// <summary>杜拉罕形态下使用的 cue 集合；返回 null 视为没有变形资源，dispatcher 回落到默认 <see cref="IModCharacterVisualCues.VisualCues"/>。</summary>
    VisualCueSet? DullahanVisualCues { get; }
}
