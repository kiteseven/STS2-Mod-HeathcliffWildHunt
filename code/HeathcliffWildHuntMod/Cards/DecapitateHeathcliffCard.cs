using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HeathcliffWildHuntMod.Powers;
using HeathcliffWildHuntMod.Relics;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HeathcliffWildHuntMod.Cards;

/// <summary>斩首希斯克利夫：1费能力。回合末回10理智，受击攒临时力量，低理智/杜拉罕+3力量-1敏捷。</summary>
public sealed class DecapitateHeathcliffCard : CardModel
{
    protected override HashSet<CardTag> CanonicalTags => new();
    
    /// <summary>风味悬浮框：检测 CleanAllCathy 决定显示抹消前/后文本。</summary>
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    {
        // ⚠️ CardModel.Owner getter 有 AssertMutable()，图鉴里是 canonical 实例 → 先检查 IsMutable
        new HoverTip(new LocString("cards", !base.IsMutable
            ? "DECAPITATE_HEATHCLIFF_CARD.flavor"
            : (base.Owner?.Relics.Any(r => r is CleanAllCathyRelic) == true
                ? "DECAPITATE_HEATHCLIFF_CARD.flavorErasured" : "DECAPITATE_HEATHCLIFF_CARD.flavor"))),
    };
    public DecapitateHeathcliffCard() : base(1, CardType.Power, CardRarity.Rare, TargetType.Self) { }
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
        => await PowerCmd.Apply<DecapitateHeathcliffPower>(ctx, base.Owner.Creature, 1, base.Owner.Creature, this);
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
