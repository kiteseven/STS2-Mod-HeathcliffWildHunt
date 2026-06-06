using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
namespace HeathcliffWildHuntMod.Cards;
public sealed class ThoughtGather : CardModel
{
    protected override HashSet<CardTag> CanonicalTags => new();
    public ThoughtGather() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { }
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
        => await CardPileCmd.Draw(ctx, 2, base.Owner);
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}