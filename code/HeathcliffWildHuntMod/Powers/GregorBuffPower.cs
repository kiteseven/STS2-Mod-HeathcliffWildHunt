using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HeathcliffWildHuntMod.Powers;

/// <summary>格里高尔随从被动：存活期间，每 5 点敌人最高沉沦强度给狂猎 +1 力量（最多 +8）。
/// 动态追踪：每当任意敌人身上的沉沦强度发生变化，自动重新计算力量值并补差。随从死亡时移除全部加成。</summary>
public sealed class GregorBuffPower : PowerModel
{
    private Creature? _player;
    private int _appliedStrength;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.None;

    public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        // 随从是怪物型 Creature，Player 永远为 null；必须用 PetOwner 追溯到所属玩家
        _player = base.Owner.PetOwner?.Creature;
        if (_player == null) return;

        // 初始结算
        var targetStrength = CalcTargetStrength();
        _appliedStrength = targetStrength;
        if (_appliedStrength > 0)
            await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), _player, _appliedStrength, _player, cardSource);
    }

    /// <summary>当任意敌人的沉沦强度变化时，重新计算并补差力量。</summary>
#if OFFICIAL
    // 正式版去掉首参 PlayerChoiceContext，补一个 null 局部变量保持方法体不变（ctx 仅流向 PowerCmd shim）
    public override async Task AfterPowerAmountChanged(PowerModel power, decimal amount,
        Creature? applier, CardModel? cardSource)
    {
        PlayerChoiceContext ctx = null!;
#else
    public override async Task AfterPowerAmountChanged(PlayerChoiceContext ctx, PowerModel power, decimal amount,
        Creature? applier, CardModel? cardSource)
    {
#endif
        if (_player == null || power is not SinkingPower) return;
        // 只关心敌人身上的沉沦变化（强度而非层数）
        if (!power.Owner.IsEnemy) return;

        var newTarget = CalcTargetStrength();
        var delta = newTarget - _appliedStrength;
        if (delta == 0) return;

        await PowerCmd.Apply<StrengthPower>(ctx, _player, delta, _player, cardSource);
        _appliedStrength = newTarget;
    }

    /// <summary>随从死亡 → 移除全部授予的力量</summary>
    public override async Task AfterRemoved(Creature oldOwner)
    {
        if (_player != null && _appliedStrength > 0)
        {
            await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), _player, -_appliedStrength, _player, null);
            _appliedStrength = 0;
        }
    }

    /// <summary>计算目前应给的力量：敌人中最高沉沦强度 ÷ 5，上限 8。</summary>
    private int CalcTargetStrength()
    {
        if (_player == null) return 0;
        int maxIntensity = _player.CombatState.Creatures
            .Where(c => c.IsEnemy && c.IsAlive)
            .Select(c => c.GetPower<SinkingPower>()?.Intensity ?? 0)
            .DefaultIfEmpty(0).Max();
        return System.Math.Min(8, maxIntensity / 5);
    }
}
