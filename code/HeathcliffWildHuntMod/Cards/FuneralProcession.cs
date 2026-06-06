using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HeathcliffWildHuntMod.Cards;

/// <summary>C13 葬列肃立：1费技能8(11)格挡。</summary>
public sealed class FuneralProcession : CardModel
{
    private const decimal BaseBlock = 8m;
    private const decimal UpgradeBlockBy = 3m;

    public override bool GainsBlock => true;
    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Defend };
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new BlockVar(BaseBlock, ValueProp.Move) };

    public FuneralProcession() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await CreatureCmd.GainBlock(base.Owner.Creature, DynamicVars.Block, play);
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(UpgradeBlockBy);
}
