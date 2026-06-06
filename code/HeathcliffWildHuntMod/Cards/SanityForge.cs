using System.Collections.Generic;
using System.Threading.Tasks;
using HeathcliffWildHuntMod.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HeathcliffWildHuntMod.Cards;

/// <summary>理性熔铸：0费技能，失去10理智，获得2(3)能量。消耗。</summary>
public sealed class SanityForge : CardModel
{
    private const int SanityCost = 10;
    private const int EnergyGain = 2;

    protected override HashSet<CardTag> CanonicalTags => new();
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    public SanityForge() : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var owner = base.Owner.Creature;
        await SanityPower.Drain(ctx, owner, SanityCost, owner, this);
        await PlayerCmd.GainEnergy(EnergyGain, base.Owner);
    }

    protected override void OnUpgrade() => RemoveKeyword(CardKeyword.Exhaust);
}
