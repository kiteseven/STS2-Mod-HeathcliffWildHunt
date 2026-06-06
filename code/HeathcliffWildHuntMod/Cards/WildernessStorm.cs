using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HeathcliffWildHuntMod.Cards;

/// <summary>U21 荒原疾驰：1费技能10(13)格挡，升级抽2。</summary>
public sealed class WildernessStorm : CardModel
{
    private const decimal BaseBlock = 10m;
    private const decimal UpgradeBlockBy = 3m;
    private const int DrawBase = 0;
    private const int DrawUp = 2;
    private int _draw = DrawBase;

    public override bool GainsBlock => true;
    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Defend };
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new BlockVar(BaseBlock, ValueProp.Move) };

    public WildernessStorm() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var o = base.Owner.Creature;
        await CreatureCmd.GainBlock(o, DynamicVars.Block, play);
        if (_draw > 0)
            await CardPileCmd.Draw(ctx, _draw, base.Owner);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(UpgradeBlockBy);
        _draw += DrawUp;
    }
}
