using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
namespace HeathcliffWildHuntMod.Cards;
/// <summary>C? 荒原疾驰：1费技能10格挡，抽1(2+)。</summary>
public sealed class WastelandDash : CardModel {
    private const decimal BaseBlock = 10m;
    private const decimal UpgradeBlockBy = 0m;
    private const int DrawBase = 1;
    private const int DrawUp = 1;
    private int _draw = DrawBase;
    public override bool GainsBlock => true;
    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Defend };
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new BlockVar(BaseBlock, ValueProp.Move) };
    public WastelandDash() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { }
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play) {
        await CreatureCmd.GainBlock(base.Owner.Creature, DynamicVars.Block, play);
        await CardPileCmd.Draw(ctx, _draw, base.Owner);
    }
    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(UpgradeBlockBy);
        _draw += DrawUp;
    }
}
