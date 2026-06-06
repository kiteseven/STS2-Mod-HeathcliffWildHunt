using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HeathcliffWildHuntMod.Relics;
using HeathcliffWildHuntMod.Compat;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HeathcliffWildHuntMod.Powers;

/// <summary>
/// 杜拉罕 Dullahan（玩家形态 buff，1–3 层）。
/// 每层效果：+3 力量, -1 敏捷。
/// <para>
/// 回合末（仅当 Amount &gt; 0 时触发，<b>按顺序</b>执行）：
///   <list type="number">
///     <item>若 <see cref="SanityPower"/>.Amount &lt;= <see cref="RemoveAllSanityThreshold"/>(-25) → <b>强退全部杜拉罕层</b>，return；</item>
///     <item>否则扣理智 <see cref="SanityDrainBase"/> = max(10, 15 − 棺÷2)（杜拉罕形态的代价）；</item>
///     <item>然后 +1 层（下回合生效，受 <see cref="Cap"/> 限制）。</item>
///   </list>
/// </para>
/// </summary>
public sealed class DullahanPower : PowerModel
{
    public const int Cap = 3;
    /// <summary>回合末若理智不高于此阈值，则解除全部杜拉罕层（替代后续 +1 与扣理智）。</summary>
    public const int RemoveAllSanityThreshold = -25;
    /// <summary>回合末扣理智基数：实际扣减 = max(<see cref="SanityDrainFloor"/>, Base − 棺÷2)。</summary>
    public const int SanityDrainBase = 15;
    /// <summary>回合末扣理智的最低保底量（与棺数无关）。</summary>
    public const int SanityDrainFloor = 10;
    /// <summary>玩家回合开始检查"塞入悲叹"的理智阈值：&gt;= 此值时塞 1 张 [[HeathcliffWildHuntMod.Cards.Lament]] 入手牌。</summary>
    public const int LamentSpawnSanityThreshold = 15;

    // 注：贴图路径不在这里覆盖。STS2 没有 PowerAssetProfile 这个类型；
    //     PowerModel 走 atlas 命名约定（power_atlas 里 `dullahan_power` 这个 sprite），
    //     找不到时 vanilla 会回退到 missing_power.png 并打 warning，不会崩。
    private int _strengthGifted;
    private int _dexterityPenalty;
    // 上一次 SyncStatBuffs 时记录的层数；用来识别 0->正 / 正->0 的形态切换瞬间。
    // PowerModel 实例每场战斗会重建，因此本字段天然每战重置——满足"每场战斗开局回到默认形态"。
    private int _lastObservedAmount;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

#if OFFICIAL
    // 正式版去掉首参 PlayerChoiceContext，补一个 null 局部变量保持方法体不变（ctx 仅流向 PowerCmd shim）
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

        await SyncStatBuffs(choiceContext, cardSource);

        // 入场扣 5 理智（从常态骑上杜拉罕的精神冲击）
        if (_lastObservedAmount <= 0 && base.Amount > 0)
            await SanityPower.Drain(choiceContext, base.Owner, 5, base.Owner, cardSource);
        // 入场音效改到 HandleFormTransition 里，等 RideStart 动画播完再放（见该方法）

        HandleFormTransition();
        _lastObservedAmount = base.Amount;
    }

    /// <summary>杜拉罕形态下攻击时给目标 +1 沉沦强度（无层数先给 1 层）。</summary>
    public override async Task AfterDamageGiven(PlayerChoiceContext ctx, Creature? dealer, DamageResult result,
        MegaCrit.Sts2.Core.ValueProps.ValueProp props, Creature target, CardModel? cardSource)
    {
        if (dealer != base.Owner || !props.HasFlag(MegaCrit.Sts2.Core.ValueProps.ValueProp.Move)) return;
        // 杜拉罕攻击：给目标加 1 强度沉沦（无层数则同时补 1 层）。强度纯累加，统一走 SinkingPower.Apply。
        var sinking = target.GetPower<SinkingPower>();
        await SinkingPower.Apply(ctx, target, layers: sinking is null ? 1 : 0, intensity: 1, base.Owner, cardSource);
        // 击杀 +1 棺
        if (result.WasTargetKilled && target.IsEnemy)
            await CoffinPower.Gain(ctx, base.Owner, 1, cardSource);
    }

    /// <summary>处理 0&lt;->正 的形态切换：派发对应 trigger 给玩家 NCreature，由 vanilla Spine animator 切到 -d 形态动画。</summary>
    private void HandleFormTransition()
    {
        var node = base.Owner.GetCreatureNode();
        // 战斗外（商店/事件直接给层）拿不到 NCreature——跳过即可，下次进战斗自然走当前层数对应形态
        if (node == null) return;

        // 0 -> 正：触发 ride-start-dullahan 变身演出（Spine animator 播完转入 idle_loop-d）
        if (_lastObservedAmount <= 0 && base.Amount > 0)
        {
            node.SetAnimationTrigger("RideStart");

            // 入场音效：等 RideStart 动画播完再放（三个骑乘音随机一个）。
            // 触发后查当前动画时长作为延迟；查不到就给个保守默认值。
            float animLen = 0f;
            try { animLen = node.GetCurrentAnimationLength(); } catch { animLen = 0f; }
            if (animLen <= 0f) animLen = 1.0f; // 兜底延迟
            HeathcliffWildHuntMod.Audio.WildHuntAudioService.PlayOneShotDelayed(
                HeathcliffWildHuntMod.Audio.WildHuntSfx.RideDullahan, animLen);
            return;
        }

        // 正 -> 0：层数清空，派发 Idle；animator 的 condition 分流此时判定 DullahanPower==0，切回默认形态 idle_loop。
        if (_lastObservedAmount > 0 && base.Amount <= 0)
        {
            node.SetAnimationTrigger("Idle");
        }
    }

    private async Task SyncStatBuffs(PlayerChoiceContext ctx, CardModel? source)
    {
        int wantStrength = base.Amount * 3;
        int wantDexterityPenalty = base.Amount;

        int strengthDelta = wantStrength - _strengthGifted;
        if (strengthDelta != 0)
        {
            await PowerCmd.Apply<StrengthPower>(ctx, base.Owner, strengthDelta, base.Owner, source);
            _strengthGifted = wantStrength;
        }

        int dexterityDelta = wantDexterityPenalty - _dexterityPenalty;
        if (dexterityDelta != 0)
        {
            await PowerCmd.Apply<DexterityPower>(ctx, base.Owner, -dexterityDelta, base.Owner, source);
            _dexterityPenalty = wantDexterityPenalty;
        }
    }

    /// <summary>
    /// 回合结束时：①先判强退（理智 ≤ -25 → 解除杜拉罕），若没退则 ②扣理智 + ③下回合+1层。
    /// </summary>
    // 两版 dll 实测都用 BeforeTurnEndVeryEarly(ctx, side)（无 participants）；反编译树里的 BeforeSideTurnEndVeryEarly 与 dll 不符，统一用 BeforeTurnEndVeryEarly。
    public override async Task BeforeTurnEndVeryEarly(
        PlayerChoiceContext choiceContext, CombatSide side)
    {
        var participants = base.Owner.CombatState?.GetCreaturesOnSide(side) ?? (IReadOnlyList<Creature>)System.Array.Empty<Creature>();
        if (base.Amount <= 0) return;
        if (!participants.Contains(base.Owner)) return;

        var sanity = base.Owner.GetPower<SanityPower>();
        int sanityValue = sanity?.Amount ?? 0;

        // ① 先判强退：理智不高于 -25 → 解除杜拉罕（除非持有 CleanAllCathyRelic——凯茜已抹消，狂猎形态锁死）
        var hasCathyRelic = base.Owner.Player?.Relics.Any(r => r is CleanAllCathyRelic) ?? false;
        if (sanityValue <= RemoveAllSanityThreshold && !hasCathyRelic)
        {
            await PowerCmd.ModifyAmount(choiceContext, this, -base.Amount, base.Owner, null);
            return;
        }

        // ② 扣理智 (15 - 棺/2, 最少 10)
        int coffin = base.Owner.GetPowerAmount<CoffinPower>();
        int drain = System.Math.Max(SanityDrainFloor, SanityDrainBase - coffin / 2);
        await SanityPower.Drain(choiceContext, base.Owner, drain, base.Owner, null);

        // ③ 下回合 +1 层
        if (base.Amount < Cap)
            await PowerCmd.ModifyAmount(choiceContext, this, 1, base.Owner, null);
    }

    /// <summary>
    /// 玩家回合开始 → 塞悲叹。条件：理智 ≥ 15 或持有 CleanAllCathy 遗物（抹消后悲叹常态化，但代价是 2 费）。
    /// </summary>
    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != base.Owner.Player) return;
        if (base.Amount <= 0) return;

        var sanity = base.Owner.GetPower<SanityPower>();
        int sanityValue = sanity?.Amount ?? 0;
        bool hasCathyRelic = player.Relics.Any(r => r is CleanAllCathyRelic);
        if (!hasCathyRelic && sanityValue < LamentSpawnSanityThreshold) return;

        var combatState = player.Creature.CombatState;
        if (combatState == null) return;

        CardModel lament;
        try
        {
            lament = combatState.CreateCard(ModelDb.Card<HeathcliffWildHuntMod.Cards.Lament>(), player);
        }
        catch (System.Exception ex)
        {
            Godot.GD.PrintErr($"[WildHunt] DullahanPower: 创建 Lament 失败: {ex.Message}");
            return;
        }

        await CompatCmd.AddGeneratedCardToCombat(lament, PileType.Hand, player, CardPilePosition.Top);
    }
}
