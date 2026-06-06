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
/// 爱与憎（过渡期——爱转恨的撕裂）：攻防一体，理智正负决定了回血还是自噬。
/// 回合末按理智正负回/扣。命中攒力敏(上限5)。理智<0 时暴怒+5力。击杀回5理智。
/// </summary>
public sealed class LoveAndHatePower : PowerModel
{
    private int _hitBonus;
    private int _tempStrApplied;
    private int _tempDexApplied;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.None;

    /// <summary>回合末：理智≥0→回10；理智<0→扣6（爱转恨，开始自噬）。</summary>
    // 两版 dll 实测都用 BeforeTurnEnd(ctx, side)（无 participants）；反编译树里的 BeforeSideTurnEnd 与 dll 不符，统一用 BeforeTurnEnd。
    public override async Task BeforeTurnEnd(PlayerChoiceContext ctx, CombatSide side)
    {
        var participants = base.Owner.CombatState?.GetCreaturesOnSide(side) ?? (IReadOnlyList<Creature>)System.Array.Empty<Creature>();
        if (!participants.Contains(base.Owner)) return;
        var sv = base.Owner.GetPower<SanityPower>()?.Amount ?? 0;
        if (sv >= 0)
            await SanityPower.Restore(ctx, base.Owner, 10, base.Owner, null);
        else
            await SanityPower.Drain(ctx, base.Owner, 6, base.Owner, null);
        if (_tempStrApplied > 0) { await PowerCmd.Apply<StrengthPower>(ctx, base.Owner, -_tempStrApplied, base.Owner, null); _tempStrApplied = 0; }
        if (_tempDexApplied > 0) { await PowerCmd.Apply<DexterityPower>(ctx, base.Owner, -_tempDexApplied, base.Owner, null); _tempDexApplied = 0; }
    }

    /// <summary>攻击命中 → 攒力敏(上限3)；击杀 → +5 理智（宣泄）。</summary>
    public override async Task AfterDamageGiven(PlayerChoiceContext ctx, Creature? dealer, DamageResult result,
        ValueProp props, Creature target, CardModel? cardSource)
    {
        if (dealer != base.Owner) return;
        if (!props.HasFlag(ValueProp.Move)) return;
        if (result.UnblockedDamage > 0 && _hitBonus < 3) _hitBonus++;
        if (result.WasTargetKilled && target.IsEnemy)
            await SanityPower.Restore(ctx, base.Owner, 5, base.Owner, cardSource);
    }

    /// <summary>回合开始：发临时力敏。理智<0 → +3 暴怒力量。</summary>
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext ctx, Player player)
    {
        if (player != base.Owner.Player) return;
        int str = _hitBonus, dex = _hitBonus;
        _hitBonus = 0;
        var sv = base.Owner.GetPower<SanityPower>()?.Amount ?? 0;
        if (sv < 0) str += 3;
        if (str > 0) { await PowerCmd.Apply<StrengthPower>(ctx, base.Owner, str, base.Owner, null); _tempStrApplied = str; }
        if (dex > 0) { await PowerCmd.Apply<DexterityPower>(ctx, base.Owner, dex, base.Owner, null); _tempDexApplied = dex; }
    }
}
