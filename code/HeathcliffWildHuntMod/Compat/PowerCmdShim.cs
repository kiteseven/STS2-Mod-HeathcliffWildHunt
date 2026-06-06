using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
// 真正的游戏命令类（被本 shim 转发），用别名避免与本类的全局别名 PowerCmd 冲突
using GamePowerCmd = MegaCrit.Sts2.Core.Commands.PowerCmd;

namespace HeathcliffWildHuntMod.Compat;

/// <summary>
/// <c>PowerCmd</c> 的双版本转发壳。业务侧 94 处调用沿用测试版写法
/// <c>PowerCmd.Apply(ctx, target, amount, applier, cardSource)</c> 不变（经全局别名指到这里）。
/// <list type="bullet">
///   <item><b>Beta</b>：原样把首参 <paramref name="choiceContext"/> 转发给游戏 PowerCmd（行为零回归）。</item>
///   <item><b>OFFICIAL</b>：正式版 PowerCmd 删除了首参 PlayerChoiceContext，这里把 ctx 吞掉再转发。</item>
/// </list>
/// 只实现业务实际用到的重载（泛型 <c>Apply&lt;T&gt;</c> 单目标 + <c>ModifyAmount</c>，及一个泛型多目标兜底）。
/// </summary>
internal static class PowerCmdShim
{
    /// <summary>给单个目标施加/叠加一个 Power，返回该 Power 实例（amount==0 时游戏当 no-op 返回 null）。</summary>
    public static Task<T?> Apply<T>(
        PlayerChoiceContext choiceContext, Creature target, decimal amount,
        Creature? applier, CardModel? cardSource, bool silent = false) where T : PowerModel
#if OFFICIAL
        => GamePowerCmd.Apply<T>(target, amount, applier, cardSource, silent);
#else
        => GamePowerCmd.Apply<T>(choiceContext, target, amount, applier, cardSource, silent);
#endif

    /// <summary>批量目标重载（当前业务未直接用到，保留以兜底/未来复用）。</summary>
    public static Task<IReadOnlyList<T>> Apply<T>(
        PlayerChoiceContext choiceContext, IEnumerable<Creature> targets, decimal amount,
        Creature? applier, CardModel? cardSource, bool silent = false) where T : PowerModel
#if OFFICIAL
        => GamePowerCmd.Apply<T>(targets, amount, applier, cardSource, silent);
#else
        => GamePowerCmd.Apply<T>(choiceContext, targets, amount, applier, cardSource, silent);
#endif

    /// <summary>对已有 Power 实例做增量修改，返回修改后层数。</summary>
    public static Task<int> ModifyAmount(
        PlayerChoiceContext choiceContext, PowerModel power, decimal offset,
        Creature? applier, CardModel? cardSource, bool silent = false)
#if OFFICIAL
        => GamePowerCmd.ModifyAmount(power, offset, applier, cardSource, silent);
#else
        => GamePowerCmd.ModifyAmount(choiceContext, power, offset, applier, cardSource, silent);
#endif
}
