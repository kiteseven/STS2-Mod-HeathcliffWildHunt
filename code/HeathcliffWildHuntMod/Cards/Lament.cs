using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HeathcliffWildHuntMod.Combat.Cards;
using HeathcliffWildHuntMod.Powers;
using HeathcliffWildHuntMod.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HeathcliffWildHuntMod.Cards;

/// <summary>
/// 衍生卡：悲叹、哀恸、破灭吧 Lament（设计案 v2 6.6 节，杜拉罕高理智回合开始塞入手牌的虚无攻击）。
/// <para>
/// 1 费 攻击 35 (45) 伤害；目标[gold]理智[/gold] &lt; 0 时伤害翻倍。
/// </para>
/// <para>
/// 使用时（扣自身 50 理智 → 应用各项加成 → 单段攻击）：
/// <list type="bullet">
///   <item>目标每带 5 级<see cref="SinkingPower.Intensity"/>沉沦强度，基础威力 +1（最多 +4）</item>
///   <item>自身理智越低,本卡伤害 +0.3% / 1 点（最多 +21%）</item>
///   <item>自身每层<see cref="CoffinPower"/>棺,伤害 +10%（最多 +100%）</item>
///   <item>自身每层<see cref="DullahanPower"/>杜拉罕,伤害 +20%（最多 +60%）</item>
///   <item>自身获得 2 棺</item>
/// </list>
/// </para>
/// <para>
/// 攻击后：
/// <list type="bullet">
///   <item>击杀目标 → 自身 +3 棺</item>
///   <item>自身理智 &lt; 0 → 恢复 10 理智 + 每差 1 点理智额外 +2 恢复（合计上限 50）</item>
/// </list>
/// </para>
/// <para>
/// 回合结束时：解除自身全部杜拉罕。<br/>
/// 关键字：虚无（未打出回合末消耗）+ 消耗（打出后消耗）。
/// </para>
/// </summary>
public sealed class Lament : CardModel
{
    // 基础伤害与升级量（来自设计案 6.6 衍生卡表）
    private const decimal BaseDamage = 16m;
    private const decimal UpgradeDamageBy = 8m;

    // 使用时自损理智量
    private const int SelfSanityCost = 50;
    // 使用时获得棺数
    private const int SelfCoffinGainOnUse = 2;
    // 击杀返棺
    private const int CoffinGainOnKill = 3;

    // 沉沦加成：每 5 强度 +1 基础威力,最多 +4 → 强度 ≥ 20 触顶
    private const int SinkingPerStep = 5;
    private const int SinkingMaxBonus = 4;

    // 理智越低伤害加成：每 -1 +0.3%,最多 +21%
    private const decimal SanityLowPctPerPoint = 0.003m;   // 0.3%
    private const decimal SanityLowPctMax     = 0.21m;     // 21%

    // 棺加成：每层 +10%,最多 +100%(10 层)
    private const decimal CoffinPctPerStack = 0.10m;
    private const decimal CoffinPctMax      = 1.00m;

    // 杜拉罕加成：每层 +20%,最多 +60%(3 层)
    private const decimal DullahanPctPerStack = 0.20m;
    private const decimal DullahanPctMax      = 0.60m;

    // 理智 < 0 时恢复：10 + 每差 1 点 +2,上限 50
    private const int SanityRestoreBase = 10;
    private const int SanityRestorePerNegativePoint = 2;
    private const int SanityRestoreMax = 50;

    // panic 翻倍：目标理智 < 0 时,基础威力倍率(在所有加成应用之后再 ×2)
    private const decimal PanicDamageMultiplier = 2m;

    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Strike };

    /// <summary>虚无（未打出回合末消耗）+ 消耗（打出后消耗）。</summary>
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Ethereal, CardKeyword.Exhaust };

    // 注：CanBeGeneratedInCombat 保持默认 true——CardPileCmd.AddGeneratedCardToCombat 内部不校验这个，
    //     但 CardFactory 会用到它做牌池筛选。悲叹不是牌池随机产物（由 DullahanPower 显式 spawn），
    //     所以这里不干预；CardRarity.Token 本身就足够防止进入奖励随机池。

    /// <summary>
    /// 挂 <see cref="ComputedDamageVar"/>：卡面 {Damage:diff()} 不再是固定基础值，而是按
    /// 「当前卡牌 + 当前指向目标」实时算出含全部自定义加成的有效基础伤害，再由引擎跑增伤 hook。
    /// 这样指向不同敌人（沉沦强度不同、是否 panic）时卡面数字会随之变化，与实战口径一致。
    /// </summary>
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[]
        {
            new ComputedDamageVar(BaseDamage, ValueProp.Move,
                // 预览委托：target 为玩家当前悬停的敌人（可为 null）。
                // previewMode=true 表示这是预览语境——此时自身理智「尚未」扣那 50 点，
                // 需模拟扣后值，让显示的伤害与真正打出后一致。
                (card, target) => ComputeEffectiveBaseDamage(card, target, isPreview: true)),
        };

    /// <summary>
    /// 计算「预 vanilla 增伤 hook 的有效基础伤害」——OnPlay 实战与卡面预览共用，避免两处算法漂移。
    /// </summary>
    /// <param name="card">本卡（mutable 实例；canonical 由调用方挡掉）。</param>
    /// <param name="target">攻击目标。预览时为悬停敌人，可为 null（未指向时按无目标算，沉沦/ panic 加成记 0）。</param>
    /// <param name="isPreview">
    /// true=预览语境（自身 50 理智尚未扣，需在本方法内模拟扣后理智来算"理智越低伤害越高"）；
    /// false=实战语境（OnPlay 已先行 Drain 50 理智，直接读当前理智即可）。
    /// </param>
    private static decimal ComputeEffectiveBaseDamage(CardModel card, Creature? target, bool isPreview)
    {
        var owner = card.Owner.Creature;

        // 起算 = 卡牌当前 Damage 基础值（含升级 +8）
        decimal damage = card.DynamicVars.Damage.BaseValue;

        // 2.1 目标沉沦强度加成（基础伤害 +1 / 5 强度,最多 +4）；无目标时记 0
        var sinking = target?.GetPower<SinkingPower>();
        int sinkingIntensity = sinking?.Intensity ?? 0;
        damage += Math.Min(SinkingMaxBonus, sinkingIntensity / SinkingPerStep);

        // 2.2 自身理智越低 → 百分比加成(每 -1 +0.3%,最多 +21%)
        //     预览语境要模拟"扣掉 50 理智后"的值；实战语境 OnPlay 已扣，直接读当前值。
        var ownerSanity = owner.GetPower<SanityPower>();
        int ownerSanityValue = ownerSanity?.Amount ?? 0;
        if (isPreview)
            ownerSanityValue = Math.Max(SanityPower.MinSanity, ownerSanityValue - SelfSanityCost);
        if (ownerSanityValue < 0)
        {
            decimal pct = Math.Min(SanityLowPctMax, SanityLowPctPerPoint * (-ownerSanityValue));
            damage *= (1m + pct);
        }

        // 2.3 自身棺层数 +10%/层（最多 +100%）
        //     预览时棺也未发放那 +2（OnPlay 第 3 步才给），但棺加成在伤害结算前已读旧值，故两边都用当前棺数。
        int coffinAmount = owner.GetPowerAmount<CoffinPower>();
        damage *= (1m + Math.Min(CoffinPctMax, CoffinPctPerStack * coffinAmount));

        // 2.4 自身杜拉罕层数 +20%/层（最多 +60%）
        int dullahanAmount = owner.GetPowerAmount<DullahanPower>();
        damage *= (1m + Math.Min(DullahanPctMax, DullahanPctPerStack * dullahanAmount));

        // 2.5 目标 panic（理智 < 0）→ 在所有加成之后 ×2；无目标时不翻倍
        var targetSanity = target?.GetPower<SanityPower>();
        if (targetSanity is not null && targetSanity.Amount < 0)
            damage *= PanicDamageMultiplier;

        // 取整避免浮点累积偏差（向下取整,与 vanilla DamageVar 行为一致）
        return Math.Floor(damage);
    }

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            var tips = new List<IHoverTip>
            {
                new HoverTip(new LocString("cards", "LAMENT.title"), new LocString("cards", "LAMENT.detailed.use")),
                new HoverTip(new LocString("cards", "LAMENT.title"), new LocString("cards", "LAMENT.detailed.scaling")),
                new HoverTip(new LocString("cards", "LAMENT.title"), new LocString("cards", "LAMENT.detailed.after")),
                new HoverTip(new LocString("cards", "LAMENT.title"), new LocString("cards", "LAMENT.detailed.end")),
                // ⚠️ CardModel.Owner 的 getter 有 AssertMutable()，canonical 实例（如图鉴显示）
                // 访问会抛 CanonicalModelException。图鉴里始终显示默认风味文本。
                new HoverTip(new LocString("cards", !base.IsMutable
                    ? "LAMENT.flavor"
                    : (base.Owner?.Relics.Any(r => r is CleanAllCathyRelic) == true
                        ? "LAMENT.flavorErasured" : "LAMENT.flavor"))),
                HoverTipFactory.FromPower<SanityPower>(),
                HoverTipFactory.FromPower<SinkingPower>(),
                HoverTipFactory.FromPower<CoffinPower>(),
                HoverTipFactory.FromPower<DullahanPower>(),
            };
            return tips;
        }
    }

    public Lament() : base(2, CardType.Attack, CardRarity.Token, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        var owner = base.Owner.Creature;
        var target = play.Target;

        // ── 第 1 步：使用时扣自身 50 理智（在算伤害之前扣,让"自身理智越低伤害越高"读到扣后值,放大特性意味）──
        await SanityPower.Drain(ctx, owner, SelfSanityCost, owner, this);

        // ── 第 2 步：算总伤害 ─────────────────────────────────────────────
        // 与卡面预览共用 ComputeEffectiveBaseDamage：此处 isPreview=false——理智已在第 1 步实扣，直接读当前值。
        decimal damage = ComputeEffectiveBaseDamage(this, target, isPreview: false);

        // ── 第 3 步：使用时 +2 棺（伤害结算前发放,让本回合后续卡读到新棺数）──
        await CoffinPower.Gain(ctx, owner, SelfCoffinGainOnUse, this);

        // ── 第 4 步：发出单段攻击,获取 DamageResult ──────────────────────
        var attack = await DamageCmd.Attack(damage)
            .FromCard(this).Targeting(target)
            .WithAttackerAnim(HeathcliffAttackAnim.Attack3, HeathcliffAttackAnim.Attack3HitDelay)
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(ctx);

        // ── 第 5 步：攻击后效果 ─────────────────────────────────────────
        // 5.1 击杀返 3 棺
        // 两版真实 dll 的 attack.Results 都是扁平 IEnumerable<DamageResult>（旧反编译树里的 List<DamageResult> 嵌套与 dll 不符），直接 Any 即可。
        bool killed = attack.Results.Any(r => r.WasTargetKilled);
        if (killed)
        {
            await CoffinPower.Gain(ctx, owner, CoffinGainOnKill, this);
        }

        // 5.2 自身理智 < 0 → 补偿恢复（基础 10 + 每差 1 点 +2,上限 50）
        //     注意：用扣理智之后的当前值算（前面 SanityPower.Drain 已经扣过 50）
        var ownerSanityNow = owner.GetPower<SanityPower>();
        int ownerSanityNowValue = ownerSanityNow?.Amount ?? 0;
        if (ownerSanityNowValue < 0)
        {
            int restore = SanityRestoreBase + (-ownerSanityNowValue) * SanityRestorePerNegativePoint;
            if (restore > SanityRestoreMax) restore = SanityRestoreMax;
            await SanityPower.Restore(ctx, owner, restore, owner, this);
        }

        // ── 第 6 步：回合结束时退出杜拉罕 ─────────────────────────────────
        // 实现：本卡打出后立刻把"待执行的退杜拉罕"标记到 [[HeathcliffStatePower]] 上,
        //      由 StatePower 在 BeforeSideTurnEnd 时统一移除杜拉罕层。
        //      没用 Apply 立即移除,是为了让本回合余下卡牌仍能享受杜拉罕加成（设计案"打出 S3 后退出杜拉罕"语义）。
        var state = owner.GetPower<HeathcliffStatePower>();
        if (state is not null)
        {
            state.RequestRemoveDullahanAtTurnEnd = true;
        }
        else
        {
            // 兜底：没有 StatePower 时直接立即移除(理论上不会触发)
            var dullahan = owner.GetPower<DullahanPower>();
            if (dullahan is not null && dullahan.Amount > 0)
            {
                await PowerCmd.ModifyAmount(ctx, dullahan, -dullahan.Amount, owner, this);
            }
        }
    }

    /// <summary>升级：基础伤害 +10（35 → 45）。</summary>
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(UpgradeDamageBy);
}
