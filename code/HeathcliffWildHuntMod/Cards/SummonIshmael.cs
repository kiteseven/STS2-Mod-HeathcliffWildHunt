using System.Collections.Generic;
using System.Threading.Tasks;
using HeathcliffWildHuntMod.Commands;
using HeathcliffWildHuntMod.Monsters;
using HeathcliffWildHuntMod.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
namespace HeathcliffWildHuntMod.Cards;
public sealed class SummonIshmael : CardModel
{
    private const int CoffinCost = 2;
    protected override HashSet<CardTag> CanonicalTags => new();
    public SummonIshmael() : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var o = base.Owner.Creature;
        await CoffinPower.Consume(ctx, o, CoffinCost, this);
        await MinionCmdHelper.Summon<IshmaelMinion>(base.Owner, IsUpgraded, this);
    }
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
