using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HeathcliffWildHuntMod.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
namespace HeathcliffWildHuntMod.Cards;
/// <summary>U? 葬土守候：2费技能20(26)格挡，给全体敌人3(5+)层1强度沉沦，下回合获得17格挡。</summary>
public sealed class MourningWall : CardModel
{
    private const decimal BaseBlock = 20m;
    private const decimal UpBlock = 6m;
    private const int SinkBase = 3;
    private const int SinkUp = 2;
    private const int NextTurnBlock = 17;
    private int _sink = SinkBase;
    public override bool GainsBlock => true;
    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Defend };
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new BlockVar(BaseBlock, ValueProp.Move) };
    public MourningWall() : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var o = base.Owner.Creature;
        await CreatureCmd.GainBlock(o, DynamicVars.Block, play);
        var enemies = play.Target!.CombatState!.Creatures.Where(c => c.IsEnemy && c.IsAlive);
        foreach (var e in enemies)
            await SinkingPower.Apply(ctx, e, layers: _sink, intensity: 1, o, this);
        // 下回合获得17格挡
        await PowerCmd.Apply<BlockNextTurnPower>(ctx, o, NextTurnBlock, o, this);
    }
    protected override void OnUpgrade() { DynamicVars.Block.UpgradeValueBy(UpBlock); _sink += SinkUp; }
}