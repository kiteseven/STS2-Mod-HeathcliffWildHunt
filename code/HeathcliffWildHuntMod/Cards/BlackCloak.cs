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

/// <summary>C20 黑色斗篷：1费技能 10格挡 + 恢复10理智。</summary>
public sealed class BlackCloak : CardModel
{
    private const decimal BaseBlock = 10m;
    private const decimal UpgradeBlockBy = 0m;
    private const int RestoreBase = 10;
    private const int RestoreUpgrade = 0;
    private int _restore = RestoreBase;
    public override bool GainsBlock => true;
    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Defend };
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new BlockVar(BaseBlock, ValueProp.Move) };
    public BlackCloak() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { }
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var o = base.Owner.Creature;
        await CreatureCmd.GainBlock(o, DynamicVars.Block, play);
        await SanityPower.Restore(ctx, o, _restore, o, this);
    }
    protected override void OnUpgrade() { DynamicVars.Block.UpgradeValueBy(UpgradeBlockBy); _restore += RestoreUpgrade; }
}
