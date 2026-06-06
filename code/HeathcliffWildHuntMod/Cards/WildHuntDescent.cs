using System.Collections.Generic;
using System.Threading.Tasks;
using HeathcliffWildHuntMod.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HeathcliffWildHuntMod.Cards;

/// <summary>R32 狂猎降临：0费(固有+)技能，立即获得1层杜拉罕(无视理智门槛)。消耗。</summary>
public sealed class WildHuntDescent : CardModel
{
    protected override HashSet<CardTag> CanonicalTags => new();
    public override IEnumerable<CardKeyword> CanonicalKeywords => IsUpgraded ? new[] { CardKeyword.Exhaust, CardKeyword.Innate } : new[] { CardKeyword.Exhaust };

    public WildHuntDescent() : base(0, CardType.Skill, CardRarity.Rare, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await PowerCmd.Apply<DullahanPower>(ctx, base.Owner.Creature, 1, base.Owner.Creature, this);
    }

    protected override void OnUpgrade() { } // 升级变固有
}
