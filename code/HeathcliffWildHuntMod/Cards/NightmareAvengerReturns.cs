using System.Collections.Generic;
using System.Threading.Tasks;
using HeathcliffWildHuntMod.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HeathcliffWildHuntMod.Cards;

/// <summary>R16 沸腾怨念：1费能力。每次获得棺时层数+1。回合开始按层数给力量/敏捷后重置。未升级带虚无。</summary>
public sealed class NightmareAvengerReturns : CardModel
{
    protected override HashSet<CardTag> CanonicalTags => new();
    public override IEnumerable<CardKeyword> CanonicalKeywords =>
        IsUpgraded ? System.Array.Empty<CardKeyword>() : new[] { CardKeyword.Ethereal };

    public NightmareAvengerReturns() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
        => await PowerCmd.Apply<NightmareAvengerReturnsPower>(ctx, base.Owner.Creature, 1, base.Owner.Creature, this);

    protected override void OnUpgrade() { } // 升级去掉虚无
}
