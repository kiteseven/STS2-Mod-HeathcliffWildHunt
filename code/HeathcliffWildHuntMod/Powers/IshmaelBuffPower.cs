using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace HeathcliffWildHuntMod.Powers;

/// <summary>以实玛利随从被动：存活期间，狂猎攻击命中时恢复 2 理智 + 施加沉沦层数 +1。</summary>
public sealed class IshmaelBuffPower : PowerModel
{
    /// <summary>狂猎（玩家）实体引用——此 buff 挂在随从身上，但效果作用于玩家。</summary>
    private Creature? _player;

    /// <summary>
    /// 防重入标志：避免"沉沦层数变化 → 本 Power 再加层 → 再次触发层数变化 → 无限递归"的死循环。
    /// AfterPowerAmountChanged 内部调用 PowerCmd.ModifyAmount 会再次触发自己的钩子，必须显式拦截。
    /// </summary>
    private bool _isProcessingSinking;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.None;

    public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        // 随从是怪物型 Creature，Player 永远为 null；必须用 PetOwner 追溯到所属玩家
        _player = base.Owner.PetOwner?.Creature;
    }

    /// <summary>狂猎造伤命中目标生命 → +2 理智</summary>
    public override async Task AfterDamageGiven(PlayerChoiceContext ctx, Creature? dealer, DamageResult result,
        ValueProp props, Creature target, CardModel? cardSource)
    {
        if (_player == null || dealer != _player) return;
        if (!props.HasFlag(ValueProp.Move) || result.UnblockedDamage <= 0) return;
        await SanityPower.Restore(ctx, _player, 2, dealer, cardSource);
    }

    /// <summary>狂猎施加沉沦时额外 +2 层（注：防重入标志避免无限递归）</summary>
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
        if (_player == null || _isProcessingSinking) return;
        if (power is not SinkingPower || power.Owner == _player) return;
        if (amount > 0 && applier == _player)
        {
            _isProcessingSinking = true;
            try
            {
                await PowerCmd.ModifyAmount(ctx, power, 2, applier, cardSource);
            }
            finally
            {
                _isProcessingSinking = false;
            }
        }
    }
}
