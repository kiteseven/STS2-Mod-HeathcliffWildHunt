using System.Collections.Generic;
using System.Threading.Tasks;
using HeathcliffWildHuntMod.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HeathcliffWildHuntMod.Cards;

/// <summary>
/// B4 杜拉罕啊……！ OhDullahan：1 费技能。
/// 效果：获得 7 (10) 格挡；理智 &gt;= 15 立即获得 1 层杜拉罕，否则回合结束获得（升级阈值降至 10）。
/// 实现：未达阈值时把层数排队到 [[HeathcliffStatePower]].PendingDullahanAtTurnEnd，
///       由 StatePower 在 AtTurnEnd 中实际应用。
/// </summary>
public sealed class OhDullahan : CardModel
{
    // 立即触发的理智阈值（&gt;= 即可触发）；升级后降到 10
    private const int BaseSanityThreshold = 15;
    private const int UpgradedSanityThreshold = 10;
    private const int DullahanGainAmount = 1;

    // 基础格挡值（升级 +3）：技能卡顺带的"勉强撑住"防御
    private const decimal BaseBlock = 7m;
    private const decimal UpgradeBlockBy = 3m;

    private int _sanityThreshold = BaseSanityThreshold;

    /// <summary>声明本卡会产生格挡，便于游戏意图系统识别。</summary>
    public override bool GainsBlock => true;

    protected override HashSet<CardTag> CanonicalTags => new();

    /// <summary>挂 BlockVar 用于卡面 {Block:diff()} 文本展示与升级追踪。</summary>
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new BlockVar(BaseBlock, ValueProp.Move) };

    /// <summary>
    /// 悬浮提示：悲叹牌预览 + 四条独立规则（带"悲叹、哀恸、破灭吧"标题前缀）+ 杜拉罕 Power。
    /// 玩家不拿到悲叹也能通过悬停"杜拉罕啊"看到完整效果文本。
    /// </summary>
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    {
        HoverTipFactory.FromCard<Lament>(),
        new HoverTip(new LocString("cards", "LAMENT.title"), new LocString("cards", "LAMENT.detailed.use")),
        new HoverTip(new LocString("cards", "LAMENT.title"), new LocString("cards", "LAMENT.detailed.scaling")),
        new HoverTip(new LocString("cards", "LAMENT.title"), new LocString("cards", "LAMENT.detailed.after")),
        new HoverTip(new LocString("cards", "LAMENT.title"), new LocString("cards", "LAMENT.detailed.end")),
        HoverTipFactory.FromPower<DullahanPower>(),
    };

    public OhDullahan() : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var owner = base.Owner.Creature;

        // 1) 先发格挡：敏捷 / 减伤等加成由 STS2 内部 GainBlock 自动结算
        await CreatureCmd.GainBlock(owner, DynamicVars.Block, play);

        // 2) 取当前理智值（无 SanityPower 视为 0）
        var sanity = owner.GetPower<SanityPower>();
        int sanityValue = sanity?.Amount ?? 0;

        // 阈值用 >= 而非 >：刚好等于阈值时也应立即触发（设计案口径）
        if (sanityValue >= _sanityThreshold)
        {
            // 满足阈值：立即获得 1 层杜拉罕
            await PowerCmd.Apply<DullahanPower>(ctx, owner, DullahanGainAmount, owner, this);
            return;
        }

        // 未达阈值：排队到回合结束统一发放，避免本回合内反复读取触发的多重 Power 回调
        var state = owner.GetPower<HeathcliffStatePower>();
        if (state is not null)
        {
            state.PendingDullahanAtTurnEnd += DullahanGainAmount;
        }
        else
        {
            // 兜底：StatePower 缺失时，仍直接发放，避免卡牌效果丢失（理论上不应触发，因为 StatePower 在战斗开始时附加）
            await PowerCmd.Apply<DullahanPower>(ctx, owner, DullahanGainAmount, owner, this);
        }
    }

    /// <summary>升级：把"立即触发"的理智门槛从 15 降到 10，格挡 +3。</summary>
    protected override void OnUpgrade()
    {
        _sanityThreshold = UpgradedSanityThreshold;
        DynamicVars.Block.UpgradeValueBy(UpgradeBlockBy);
    }
}
