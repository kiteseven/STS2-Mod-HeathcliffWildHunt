using System.Collections.Generic;
using System.Threading.Tasks;
using HeathcliffWildHuntMod.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
namespace HeathcliffWildHuntMod.Cards;
public sealed class ManorWhisper : CardModel
{
    private const int CoffinBase = 1;
    private const int CoffinUp = 1;
    private int _coffin = CoffinBase;
    protected override HashSet<CardTag> CanonicalTags => new();
    public ManorWhisper() : base(0, CardType.Skill, CardRarity.Common, TargetType.Self) { }
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await CoffinPower.Gain(ctx, base.Owner.Creature, _coffin, this);
        await CardPileCmd.Draw(ctx, 1, base.Owner);
    }
    protected override void OnUpgrade() => _coffin += CoffinUp;
}