using System.Collections.Generic;
using System.Threading.Tasks;
using HeathcliffWildHuntMod.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
namespace HeathcliffWildHuntMod.Cards;
public sealed class FuneralSilence : CardModel
{
    protected override HashSet<CardTag> CanonicalTags => new();
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    public FuneralSilence() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        int coffin = base.Owner.Creature.GetPowerAmount<CoffinPower>();
        int draw = System.Math.Min(4, coffin / 2);
        if (draw > 0) await CardPileCmd.Draw(ctx, draw, base.Owner);
    }
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}