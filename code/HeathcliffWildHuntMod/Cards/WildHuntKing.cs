using System.Collections.Generic;
using System.Threading.Tasks;
using HeathcliffWildHuntMod.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HeathcliffWildHuntMod.Cards;

/// <summary>R30 狂猎之王：1费(0费+)能力，升级后额外恢复随从四分之一血量。</summary>
public sealed class WildHuntKing : CardModel
{
    protected override HashSet<CardTag> CanonicalTags => new();
    public WildHuntKing() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        // Amount=1 基础版，Amount=2 升级版（用于在 Power 里判断是否恢复血量）
        int amount = IsUpgraded ? 2 : 1;
        await PowerCmd.Apply<WildHuntKingPower>(ctx, base.Owner.Creature, amount, base.Owner.Creature, this);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}