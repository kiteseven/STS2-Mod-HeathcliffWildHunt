using System.Collections.Generic;
using System.Threading.Tasks;
using HeathcliffWildHuntMod.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HeathcliffWildHuntMod.Cards;

/// <summary>
/// B3 空棺 EmptyCoffin：1 费技能。
/// 效果：获得 2 棺，抽 1。升级费用降为 0。
/// 设计意图：起手套牌里棺资源的核心入口，让基础卡组也能堆出棺阈值（≥3 力量、≥5 敏捷）。
/// </summary>
public sealed class EmptyCoffin : CardModel
{
    // 棺获取量与抽牌量都是固定值，不参与升级
    private const int CoffinGain = 1;
    private const int DrawCount = 1;

    protected override HashSet<CardTag> CanonicalTags => new();

    public EmptyCoffin() : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        // 通过 CoffinPower.Gain 静态方法走统一入口（自动同步 HeathcliffStatePower 的"本回合获得棺"统计）
        await CoffinPower.Gain(ctx, base.Owner.Creature, CoffinGain, this);
        // 抽 1 张：CardPileCmd.Draw(ctx, count, player) 是抽牌的统一入口
        await CardPileCmd.Draw(ctx, DrawCount, base.Owner);
    }

    /// <summary>升级：费用从 1 降到 0；数值不变。</summary>
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
