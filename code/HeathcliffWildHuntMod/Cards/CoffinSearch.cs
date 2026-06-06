using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
namespace HeathcliffWildHuntMod.Cards;
public sealed class CoffinSearch : CardModel
{
    protected override HashSet<CardTag> CanonicalTags => new();
    public CoffinSearch() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await CardPileCmd.Draw(ctx, 3, base.Owner);
        // 弃1简化：从手牌弃1
        var hand = PileType.Hand.GetPile(base.Owner);
        if (hand.Cards.Count > 0)
            await CardPileCmd.Add(hand.Cards[0], PileType.Discard.GetPile(base.Owner));
    }
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}