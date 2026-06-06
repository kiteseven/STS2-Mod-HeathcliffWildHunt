using System.Collections.Generic;
using System.Threading.Tasks;
using HeathcliffWildHuntMod.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HeathcliffWildHuntMod.Cards;

/// <summary>U24 山庄的低咛：1费技能，获得2(3)棺，抽1(2)。</summary>
public sealed class CoffinWater : CardModel
{
    private const int CoffinBase = 2;
    private const int CoffinUp = 1;
    private const int DrawBase = 1;
    private const int DrawUp = 1;
    private int _coffin = CoffinBase;
    private int _draw = DrawBase;

    protected override HashSet<CardTag> CanonicalTags => new();
    public CoffinWater() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var owner = base.Owner.Creature;
        await CoffinPower.Gain(ctx, owner, _coffin, this);
        await CardPileCmd.Draw(ctx, _draw, base.Owner);
    }

    protected override void OnUpgrade()
    {
        _coffin += CoffinUp;
        _draw += DrawUp;
    }
}
