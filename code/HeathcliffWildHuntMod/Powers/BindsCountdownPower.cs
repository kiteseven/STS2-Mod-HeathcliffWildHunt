using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HeathcliffWildHuntMod.Powers;

/// <summary>
/// 拘束·理智枷锁（玩家 debuff，倒计时型）。
/// <para>
/// 由 EGO「拘束 Binds」施加：层数 = 剩余生效回合数。每回合末扣 10 理智，然后 -1 层；层数归零自动移除。
/// （独立于沉沦——这是对自己的持续理智代价。）
/// </para>
/// </summary>
public sealed class BindsCountdownPower : PowerModel
{
    /// <summary>每回合末扣的理智量。</summary>
    public const int SanityDrainPerTurn = 10;

    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>回合末：扣 10 理智，再 -1 层（层数即剩余回合数，归零后 vanilla 自动移除 Counter 型 0 层 power）。</summary>
    // 两版 dll 实测都用 BeforeTurnEnd(ctx, side)（无 participants）；反编译树里的 BeforeSideTurnEnd 与 dll 不符，统一用 BeforeTurnEnd。
    public override async Task BeforeTurnEnd(PlayerChoiceContext ctx, CombatSide side)
    {
        var participants = base.Owner.CombatState?.GetCreaturesOnSide(side) ?? (IReadOnlyList<Creature>)System.Array.Empty<Creature>();
        if (!participants.Contains(base.Owner)) return;
        if (base.Amount <= 0) return;
        await SanityPower.Drain(ctx, base.Owner, SanityDrainPerTurn, base.Owner, null);
        await PowerCmd.ModifyAmount(ctx, this, -1m, base.Owner, null);
    }
}
