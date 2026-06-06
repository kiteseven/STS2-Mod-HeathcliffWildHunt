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
/// 斩首希斯克利夫（凯瑟琳消失后——狂猎自毁）：回合末扣理智。受击攒力。
/// 杜拉罕时 +5 力 -2 敏。击杀 +2 棺。纯玻璃大炮。
/// </summary>
public sealed class DecapitateHeathcliffPower : PowerModel
{
    private int _hitBonus;
    private int _tempStrApplied;
    private int _tempDexApplied;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.None;

    /// <summary>回合末扣 5 理智（凯瑟琳消失后的持续耗损）。</summary>
    // 两版 dll 实测都用 BeforeTurnEnd(ctx, side)（无 participants）；反编译树里的 BeforeSideTurnEnd 与 dll 不符，统一用 BeforeTurnEnd。
    public override async Task BeforeTurnEnd(PlayerChoiceContext ctx, CombatSide side)
    {
        var participants = base.Owner.CombatState?.GetCreaturesOnSide(side) ?? (IReadOnlyList<Creature>)System.Array.Empty<Creature>();
        if (!participants.Contains(base.Owner)) return;
        await SanityPower.Drain(ctx, base.Owner, 5, base.Owner, null);
        if (_tempStrApplied > 0) { await PowerCmd.Apply<StrengthPower>(ctx, base.Owner, -_tempStrApplied, base.Owner, null); _tempStrApplied = 0; }
        if (_tempDexApplied > 0) { await PowerCmd.Apply<DexterityPower>(ctx, base.Owner, -_tempDexApplied, base.Owner, null); _tempDexApplied = 0; }
    }

    /// <summary>受击 → 攒力(上限2)；击杀 → +2 棺。</summary>
    public override async Task AfterDamageGiven(PlayerChoiceContext ctx, Creature? dealer, DamageResult result,
        ValueProp props, Creature target, CardModel? cardSource)
    {
        if (dealer != base.Owner) return;
        if (props.HasFlag(ValueProp.Move) && result.UnblockedDamage > 0 && _hitBonus < 2)
            _hitBonus++;
        if (result.WasTargetKilled && target.IsEnemy)
            await CoffinPower.Gain(ctx, base.Owner, 2, cardSource);
    }

    /// <summary>回合开始：发临时力。有杜拉罕 → +3 力 -1 敏（暴走代价）。</summary>
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext ctx, Player player)
    {
        if (player != base.Owner.Player) return;
        int str = _hitBonus;
        _hitBonus = 0;
        var sv = base.Owner.GetPower<SanityPower>()?.Amount ?? 0;
        if (base.Owner.HasPower<DullahanPower>())
        {
            str += 3;
            await PowerCmd.Apply<DexterityPower>(ctx, base.Owner, -1, base.Owner, null);
            _tempDexApplied -= 1;
        }
        if (str > 0) { await PowerCmd.Apply<StrengthPower>(ctx, base.Owner, str, base.Owner, null); _tempStrApplied = str; }
    }
}
