using HarmonyLib;
using HeathcliffWildHuntMod.Visuals;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace HeathcliffWildHuntMod;

/// <summary>
/// 【已停用 / 归档】曾经拦截 <see cref="NCreature.SetAnimationTrigger(string)"/>，
/// 在角色没有 Spine 时把 trigger 派发到本 mod 的逐帧 cue 播放（<see cref="ModCreatureVisualPlayback"/>）。
/// <para>
/// 废弃原因：角色已完全转 Spine 骨骼动画（见 <see cref="Heathcliff.GenerateAnimator"/>），
/// 动画职责单轨交给 vanilla <c>CreatureAnimator</c>。cue 系统继续接管动画会与 Spine 双轨打架
/// （历史 bug：受击/攻击后形态待机错乱、火堆外某些状态切回旧贴图等），故彻底摘除其动画职责。
/// </para>
/// <para>
/// 保留方式：去掉 <c>[HarmonyPatch]</c> 特性 → 不再被 <c>WildHuntBootstrap</c> 自动发现、不会生效。
/// 文件与 <see cref="ModCreatureVisualPlayback"/>/<see cref="HeathcliffVisualCues"/>/
/// <c>CueFrameSequencePlayer</c> 一并保留，作为将来「cue → 特效播放」改造的起点
/// （特效将挂在独立特效节点上，不再碰角色 Spine 本体）。
/// </para>
/// <para>
/// 若 Spine 万一加载失败（<c>HasSpineAnimation==false</c>），现按设计走 vanilla 默认行为
/// （角色静止、无逐帧兜底）——这是有意为之的单轨取舍，便于第一时间暴露 Spine 加载问题。
/// </para>
/// </summary>
internal static class PatchNCreatureSetAnimationTrigger
{
    // 原 Prefix 已停用。保留方法体仅作历史参考，无任何调用方（类不带 [HarmonyPatch]，不会被 patch 进 NCreature）。
    // 如需临时恢复逐帧 cue 兜底：给本类加回
    //   [HarmonyPatch(typeof(NCreature), nameof(NCreature.SetAnimationTrigger), typeof(string))]
    // 并把下面方法改回 private static bool Prefix(NCreature __instance, string trigger){...} 的拦截逻辑即可。
    private static bool LegacyCuePrefix_Disabled(NCreature instance, string trigger)
    {
        // 有 Spine 的角色继续走 vanilla；TryPlayFromCreatureAnimatorTrigger 内部也会再判一次 HasSpineAnimation
        if (instance.HasSpineAnimation) return true;
        return !ModCreatureVisualPlayback.TryPlayFromCreatureAnimatorTrigger(instance, trigger);
    }
}
