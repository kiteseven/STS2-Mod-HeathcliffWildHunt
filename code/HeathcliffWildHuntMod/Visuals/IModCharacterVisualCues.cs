using HeathcliffWildHuntMod.Visuals.Definition;

namespace HeathcliffWildHuntMod.Visuals;

/// <summary>
/// 标记角色 <see cref="MegaCrit.Sts2.Core.Models.CharacterModel"/> 子类提供 cue 视觉集合。
/// <para>
/// 【职责已变更】早期由 <c>PatchNCreatureSetAnimationTrigger</c> 通过本接口拿到 <see cref="VisualCueSet"/>，
/// 在无 Spine 时走逐帧 cue 动画。角色完全转 Spine 后该 patch 已停用，本接口当前不参与动画。
/// 保留它作为将来「cue → 特效播放」改造的数据入口（角色提供特效用的 <see cref="VisualCueSet"/>）。
/// </para>
/// </summary>
public interface IModCharacterVisualCues
{
    /// <summary>该角色的 cue 集合；返回 null 视为无覆盖。当前仅供（停用的）cue 路径与将来的特效系统读取。</summary>
    VisualCueSet? VisualCues { get; }
}
