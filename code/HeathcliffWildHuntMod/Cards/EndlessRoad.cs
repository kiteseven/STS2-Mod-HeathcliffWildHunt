using System.Collections.Generic;
using System.Threading.Tasks;
using HeathcliffWildHuntMod.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
namespace HeathcliffWildHuntMod.Cards;
public sealed class EndlessRoad : CardModel
{
    private const decimal BaseBlock = 11m;
    private const decimal UpgradeBlockBy = 4m;
    public override bool GainsBlock => true;
    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Defend };
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new BlockVar(BaseBlock, ValueProp.Move) };
    public EndlessRoad() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var o = base.Owner.Creature;
        int extra = o.GetPowerAmount<DullahanPower>() * 4;
        await CreatureCmd.GainBlock(o, DynamicVars.Block, play);
        if (extra > 0) await CreatureCmd.GainBlock(o, (decimal)extra, MegaCrit.Sts2.Core.ValueProps.ValueProp.Move, play);
    }
    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(UpgradeBlockBy);
}