// 双版本兼容补丁。注意：项目里 Sts2Code 反编译树与实际引用的 sts2.dll 不完全一致——
// 经对两份真实 dll（测试版 4-28 / 正式版）做元数据探测，得到的真实差异是：
//   · RunWhenSpineReady：两版 dll 都没有（仅旧反编译树残留）→ 本文件无条件补回，供两版共用。
//   · Creature.GetCreatureNode / GetBackVfxContainer：测试版 dll 有实例方法、正式版 dll 删了
//       → 仅正式版(#if OFFICIAL)以扩展方法补回；测试版用其自带实例方法（实例方法优先于扩展方法）。
// 全部声明在 vanilla 原命名空间下，调用点 using 不变即可解析，无需改任何调用代码。
using System;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;

namespace MegaCrit.Sts2.Core.Helpers
{
    /// <summary>
    /// 补回 beta 反编译树里的 <c>SpineNodeExtensions.RunWhenSpineReady</c>（两版真实 dll 都不含此扩展）。
    /// 逻辑沿用旧源码：逐帧轮询直到骨骼就绪，再用就绪后的 <see cref="MegaAnimationState"/> 回调 <paramref name="onReady"/>。
    /// 所用基元（GetSkeleton / BoundObject / GetAnimationState / AwaitProcessFrame / TaskHelper）两版 dll 均存在。
    /// </summary>
    public static class WildHuntSpineNodeCompat
    {
        // 与旧实现一致的"等待过久"告警阈值（帧）。
        private const int SpineReadyWarnThresholdFrames = 600;

        /// <summary>等骨骼动画状态就绪后回调（fire-and-forget，内部异步轮询）。</summary>
        public static void RunWhenSpineReady(this Node host, MegaSprite sprite, Action<MegaAnimationState> onReady)
        {
            TaskHelper.RunSafely(WaitForSpineReady(host, sprite, onReady));
        }

        // 逐帧等待骨骼就绪：BoundObject 失效（节点已销毁）则中止；就绪后取 AnimationState 回调。
        private static async Task WaitForSpineReady(Node host, MegaSprite sprite, Action<MegaAnimationState> onReady)
        {
            // 就绪判定用 GetSkeleton()!=null：两版 live dll 都未暴露 IsAnimationStateReady，
            // 但 GetSkeleton 一定在（HasAnimation 即靠它）；骨骼非空即代表动画状态可取，与原 IsAnimationStateReady 内部判定等价。
            int framesWaited = 0;
            while (GodotObject.IsInstanceValid(sprite.BoundObject) && sprite.GetSkeleton() == null)
            {
                await host.AwaitProcessFrame();
                framesWaited++;
                if (framesWaited == SpineReadyWarnThresholdFrames)
                    GD.PushWarning($"{host.Name}: 等待 SpineSprite 骨骼就绪已 {framesWaited} 帧仍未完成（可能资源加载失败）。");
            }
            if (GodotObject.IsInstanceValid(sprite.BoundObject))
                onReady(sprite.GetAnimationState());
        }
    }
}

#if OFFICIAL
namespace MegaCrit.Sts2.Core.Entities.Creatures
{
    using MegaCrit.Sts2.Core.Nodes.Combat;
    using MegaCrit.Sts2.Core.Nodes.Rooms;

    /// <summary>
    /// 仅正式版：补回正式版 dll 删掉的两个 <see cref="Creature"/> 实例方法（改走 <see cref="NCombatRoom"/> 查询）。
    /// 测试版 dll 自带这两个实例方法，故测试版不编译本类（实例方法优先解析，无需扩展方法）。
    /// </summary>
    public static class WildHuntCreatureNodeCompat
    {
        /// <summary>取当前战斗里该 creature 对应的 <see cref="NCreature"/> 节点（无战斗房或不在场则 null）。</summary>
        public static NCreature? GetCreatureNode(this Creature creature)
            => NCombatRoom.Instance?.GetCreatureNode(creature);

        /// <summary>取角色身后 VFX 容器：仅当该 creature 当前在战斗房中时返回 <c>BackCombatVfxContainer</c>，否则 null。</summary>
        public static Control? GetBackVfxContainer(this Creature creature)
        {
            var room = NCombatRoom.Instance;
            if (room?.GetCreatureNode(creature) != null)
                return room.BackCombatVfxContainer;
            return null;
        }
    }
}
#endif
