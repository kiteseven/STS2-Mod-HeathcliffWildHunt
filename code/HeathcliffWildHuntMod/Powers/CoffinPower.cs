using System.Linq;
using System.Collections.Generic;
using System;
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
/// 棺 Coffin（玩家资源 0–10）。阈值被动：
/// <list type="bullet">
///   <item>每 3 层 → +2 力量（本回合，最多 6）</item>
///   <item>每 4 层 → +1 敏捷（本回合，最多 2）</item>
///   <item>每 5 层 → +1 能量上限（该场战斗永久，最多 2）</item>
///   <item>≥8 → 首次出牌额外抽 1</item>
///   <item>=10 → 解锁 Boss EGO</item>
/// </list>
/// </summary>
public sealed class CoffinPower : PowerModel
{
    public const int Cap = 10;
    public const int ExtraDrawThreshold = 8;
    public const int FullThreshold = 10;

    // 每回合浮动 bonus：按层数÷3/4阶梯发放，回合开始计算差值增减
    private const int PerTurnStrPer3 = 2;
    private const int PerTurnStrMax = 6;
    private const int PerTurnDexPer4 = 1;
    private const int PerTurnDexMax = 2;

    // 每 5 层 → +1 能量上限，该场战斗永久（只增不减），最多 2
    private const int EnergyPer5 = 1;
    private const int EnergyMax = 2;

    private int _perTurnStrGifted;     // 本回合已发的每3层力量浮动 bonus
    private int _perTurnDexGifted;     // 本回合已发的每4层敏捷浮动 bonus
    private int _permanentEnergyBonus; // 该场战斗累计的能量上限加成（只增不减，最多 EnergyMax）
    private bool _drewExtraThisTurn;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public bool IsAtThreshold(int threshold) => base.Amount >= threshold;

#if OFFICIAL
    // 正式版 AfterPowerAmountChanged 去掉了首参 PlayerChoiceContext；用 null 局部变量补齐，
    // 它只会流向 PowerCmd shim（正式版下本就丢弃 ctx），故方法体保持与测试版逐字一致。
    public override async Task AfterPowerAmountChanged(
        PowerModel power, decimal amount,
        Creature? applier, CardModel? cardSource)
    {
        PlayerChoiceContext choiceContext = null!;
#else
    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext, PowerModel power, decimal amount,
        Creature? applier, CardModel? cardSource)
    {
#endif
        if (power != this) return;
        if (base.Amount > Cap)
            await PowerCmd.ModifyAmount(choiceContext, this, Cap - base.Amount, base.Owner, cardSource);

        // 每 5 层 → +1 能量上限（该场战斗永久，最多 2）：按当前层数算应得值，只增不减。
        int wantEnergy = System.Math.Min(EnergyMax, (base.Amount / 5) * EnergyPer5);
        if (wantEnergy > _permanentEnergyBonus)
            _permanentEnergyBonus = wantEnergy;

        // 棺层数变化 → 更新模型身后的紫镜特效强度（纯代码特效，随层数 0→10 递增）
        HeathcliffWildHuntMod.Visuals.CoffinAura.UpdateFor(base.Owner, base.Amount);

        // 棺层数首次达到最大层(10)→ 切换到狂猎专属战斗 BGM（每场战斗只触发一次，由控制器内部去重）
        if (base.Amount >= FullThreshold)
            HeathcliffWildHuntMod.Audio.WildHuntBgmController.OnCoffinReachedMax();
    }

    /// <summary>每 5 层棺累计的能量上限加成（该场战斗永久，只增不减，最多 2）。</summary>
    public override decimal ModifyMaxEnergy(Player player, decimal amount)
    {
        if (player != base.Owner.Player) return amount;
        return amount + _permanentEnergyBonus;
    }

    /// <summary>回合末：回收本回合发放的全部临时力量/敏捷，让 buff 真正仅当回合生效。</summary>
    // 两版 dll 实测都用 BeforeTurnEnd(ctx, side)（无 participants）；反编译树里的 BeforeSideTurnEnd 与 dll 不符，统一用 BeforeTurnEnd，
    // participants 由结束方一侧 creature 重建（语义等价：owner 仅在己方回合末命中）。
    public override async Task BeforeTurnEnd(PlayerChoiceContext choiceContext, CombatSide side)
    {
        var participants = base.Owner.CombatState?.GetCreaturesOnSide(side) ?? (IReadOnlyList<Creature>)System.Array.Empty<Creature>();
        if (!participants.Contains(base.Owner)) return;
        if (_perTurnStrGifted > 0)
        {
            await PowerCmd.Apply<StrengthPower>(choiceContext, base.Owner, -_perTurnStrGifted, base.Owner, null);
        }
        if (_perTurnDexGifted > 0)
        {
            await PowerCmd.Apply<DexterityPower>(choiceContext, base.Owner, -_perTurnDexGifted, base.Owner, null);
        }
        _perTurnStrGifted = 0;
        _perTurnDexGifted = 0;
    }

    /// <summary>回合开始：计算本回合浮动力量/敏捷 bonus，发差值。</summary>
    public override async Task AfterPlayerTurnStartEarly(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != base.Owner.Player) return;
        _drewExtraThisTurn = false;

        var amt = base.Amount;
        int wantStr  = System.Math.Min(PerTurnStrMax,   (amt / 3) * PerTurnStrPer3);
        int wantDex  = System.Math.Min(PerTurnDexMax,   (amt / 4) * PerTurnDexPer4);

        int perTurnStrDelta = wantStr - _perTurnStrGifted;
        if (perTurnStrDelta != 0)
        {
            await PowerCmd.Apply<StrengthPower>(choiceContext, base.Owner, perTurnStrDelta, base.Owner, null);
        }
        int perTurnDexDelta = wantDex - _perTurnDexGifted;
        if (perTurnDexDelta != 0)
        {
            await PowerCmd.Apply<DexterityPower>(choiceContext, base.Owner, perTurnDexDelta, base.Owner, null);
        }

        // 记录本回合已发量，下回合通过差值回退多余部分
        _perTurnStrGifted = wantStr;
        _perTurnDexGifted = wantDex;
    }

    /// <summary>≥8 棺：本回合首次出牌额外抽 1。</summary>
    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != base.Owner.Player) return;
        if (_drewExtraThisTurn) return;
        if (!IsAtThreshold(ExtraDrawThreshold)) return;

        _drewExtraThisTurn = true;
        // CardPileCmd.Draw 是抽牌入口；PlayerCmd 没有 Draw 方法
        await CardPileCmd.Draw(choiceContext, 1, base.Owner.Player);
    }

    public static async Task Gain(PlayerChoiceContext ctx, Creature owner, int amount, CardModel? source)
    {
        if (amount <= 0) return;
        await PowerCmd.Apply<CoffinPower>(ctx, owner, amount, owner, source);
        if (owner.GetPower<HeathcliffStatePower>() is { } state)
            state.CoffinGainedThisTurn += amount;
    }

    public static async Task<int> Consume(PlayerChoiceContext ctx, Creature owner, int request, CardModel? source)
    {
        var coffin = owner.GetPower<CoffinPower>();
        if (coffin is null || coffin.Amount <= 0 || request <= 0) return 0;
        int actual = Math.Min(coffin.Amount, request);
        await PowerCmd.ModifyAmount(ctx, coffin, -actual, owner, source);
        if (owner.GetPower<HeathcliffStatePower>() is { } state)
            state.CoffinConsumedThisTurn += actual;
        return actual;
    }
}
