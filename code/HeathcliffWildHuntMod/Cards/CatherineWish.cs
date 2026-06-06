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
public sealed class CatherineWish : CardModel
{
    private const decimal BaseBlock = 9m;
    private const decimal UpgradeBlockBy = 4m;
    public override bool GainsBlock => true;
    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Defend };
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new BlockVar(BaseBlock, ValueProp.Move) };
    public CatherineWish() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        await CreatureCmd.GainBlock(base.Owner.Creature, DynamicVars.Block, play);
        await CoffinPower.Gain(ctx, base.Owner.Creature, 2, this);
    }
    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(UpgradeBlockBy);
}