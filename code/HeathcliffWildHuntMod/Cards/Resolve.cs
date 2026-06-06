using System.Collections.Generic;
using System.Threading.Tasks;
using HeathcliffWildHuntMod.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;

namespace HeathcliffWildHuntMod.Cards;

/// <summary>
/// 觉悟（Ancient 先古卡，由达弗 Darv 的尘封魔典发放）：3 费 Power 卡。
/// 打出后给自身施加「靠近的勇气」<see cref="CourageNearbyPower"/>，承载全部持续被动
/// （回合末回理智、每回合 +临时力量/守护、受击回理智攒力量、低理智额外力量、有友方时不被打死）。
/// 详细效果通过悬浮框展示该 power（<see cref="ExtraHoverTips"/>），卡面只引用其名。
/// </summary>
public sealed class Resolve : CardModel
{
    protected override HashSet<CardTag> CanonicalTags => new();

    /// <summary>悬浮提示：展示被授予的「靠近的勇气」power 全部效果（标题+描述取自 COURAGE_NEARBY_POWER loc）。</summary>
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    {
        HoverTipFactory.FromPower<CourageNearbyPower>(),
    };

    public Resolve() : base(3, CardType.Power, CardRarity.Ancient, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
        => await PowerCmd.Apply<CourageNearbyPower>(ctx, base.Owner.Creature, 1, base.Owner.Creature, this);
}

