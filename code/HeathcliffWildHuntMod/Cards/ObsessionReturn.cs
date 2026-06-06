using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
namespace HeathcliffWildHuntMod.Cards;
/// <summary>U? 执念之回：0费技能，取回所有弃牌堆中的攻击牌。</summary>
public sealed class ObsessionReturn : CardModel
{
    protected override HashSet<CardTag> CanonicalTags => new();
    public ObsessionReturn() : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var discard = PileType.Discard.GetPile(base.Owner);
        var attacks = discard.Cards.Where(c => c.Type == CardType.Attack).ToList();
        foreach (var c in attacks)
            await CardPileCmd.Add(c, PileType.Hand.GetPile(base.Owner));
    }
    protected override void OnUpgrade() { } // 不升级
}