using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace HeathcliffWildHuntMod.Powers;

/// <summary>
/// 褪色的约定（凯瑟琳消失前）：纯防+续航。
/// 回合末回 20 理智。完全阻挡时攒临时敏捷。高理智给敏捷。理智不跌破 -30（约定的底线）。
/// </summary>
public sealed class FadedPromisePower : PowerModel
{
    private int _blockBonus;       // 完全阻挡次数(max3)
    private int _tempDexApplied;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.None;

    /// <summary>理智不跌破 -30——约定是最低的底线。</summary>
    public override bool AllowNegative => true;

    // 两版 dll 实测都用 BeforeTurnEnd(ctx, side)（无 participants）；反编译树里的 BeforeSideTurnEnd 与 dll 不符，统一用 BeforeTurnEnd。
    public override async Task BeforeTurnEnd(PlayerChoiceContext ctx, CombatSide side)
    {
        var participants = base.Owner.CombatState?.GetCreaturesOnSide(side) ?? (IReadOnlyList<Creature>)System.Array.Empty<Creature>();
        if (!participants.Contains(base.Owner)) return;
        // 回合末回 20 理智
        await SanityPower.Restore(ctx, base.Owner, 20, base.Owner, null);
        // 回收临时敏捷
        if (_tempDexApplied > 0)
        {
            await PowerCmd.Apply<DexterityPower>(ctx, base.Owner, -_tempDexApplied, base.Owner, null);
            _tempDexApplied = 0;
        }
    }

    /// <summary>完全阻挡伤害 → 攒 1 临时敏捷（每回合最多 3）。</summary>
    public override async Task AfterDamageReceived(PlayerChoiceContext ctx, Creature target, DamageResult result,
        ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != base.Owner) return;
        if (!props.HasFlag(ValueProp.Move)) return;
        // 完全阻挡 = 伤害全被格挡吃掉，没扣血
        if (result.UnblockedDamage > 0) return;
        if (result.BlockedDamage <= 0) return;
        if (_blockBonus >= 3) return;
        _blockBonus++;
    }

    /// <summary>回合开始：发临时敏捷。高理智额外 +2 临时敏捷。</summary>
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext ctx, Player player)
    {
        if (player != base.Owner.Player) return;
        int dex = _blockBonus;
        _blockBonus = 0;
        var sv = base.Owner.GetPower<SanityPower>()?.Amount ?? 0;
        if (sv >= 20) dex += 2;
        if (dex > 0)
        {
            await PowerCmd.Apply<DexterityPower>(ctx, base.Owner, dex, base.Owner, null);
            _tempDexApplied = dex;
        }
    }

    /// <summary>约定的底线：理智不低于 -30。</summary>
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
        if (power is not SanityPower || power.Owner != base.Owner) return;
        if (power.Amount < -30)
            await PowerCmd.ModifyAmount(ctx, power, -30 - power.Amount, base.Owner, cardSource);
    }
}
