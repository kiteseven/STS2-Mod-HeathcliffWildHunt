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

/// <summary>爱与憎：1费能力。回合末回15理智，攻击命中攒临时力敏，低理智/杜拉罕+3力量。</summary>
public sealed class LoveAndHate : CardModel
{
    protected override HashSet<CardTag> CanonicalTags => new();
    
    /// <summary>风味悬浮框：检测 CleanAllCathy 决定显示抹消前/后文本。</summary>
    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    {
        // ⚠️ CardModel.Owner getter 有 AssertMutable()，图鉴里是 canonical 实例 → 先检查 IsMutable
        new HoverTip(new LocString("cards", !base.IsMutable
            ? "LOVE_AND_HATE.flavor"
            : (base.Owner?.Relics.Any(r => r is CleanAllCathyRelic) == true
                ? "LOVE_AND_HATE.flavorErasured" : "LOVE_AND_HATE.flavor"))),
    };
    public LoveAndHate() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self) { }
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
        => await PowerCmd.Apply<LoveAndHatePower>(ctx, base.Owner.Creature, 1, base.Owner.Creature, this);
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
