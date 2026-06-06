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

/// <summary>U28 吐露哀嚎与悲伤的玻璃碎片：1费技能10(14)格挡，理智≥-10额外6格挡（理智门槛从15改为-10）。</summary>
public sealed class SpilledAgonyGlassShards : CardModel
{
    private const decimal BaseBlock = 10m;
    private const decimal UpBlock = 4m;
    private const int SanityThreshold = -10;
    private const decimal BonusBlock = 6m;

    public override bool GainsBlock => true;
    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Defend };
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new BlockVar(BaseBlock, ValueProp.Move) };

    public SpilledAgonyGlassShards() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var o = base.Owner.Creature;
        await CreatureCmd.GainBlock(o, DynamicVars.Block, play);
        var s = o.GetPower<SanityPower>();
        if (s is not null && s.Amount >= SanityThreshold)
            await CreatureCmd.GainBlock(o, BonusBlock, ValueProp.Move, play);
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(UpBlock);
}