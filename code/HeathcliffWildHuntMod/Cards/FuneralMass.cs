using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HeathcliffWildHuntMod.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HeathcliffWildHuntMod.Cards;

/// <summary>R36 葬礼弥撒：0费(0费)技能，给全体敌人5(6)层1强度沉沦。</summary>
public sealed class FuneralMass : CardModel
{
    private const int SinkBase = 5;
    private const int SinkUp = 1;
    private int _sink = SinkBase;

    protected override HashSet<CardTag> CanonicalTags => new();
    public FuneralMass() : base(0, CardType.Skill, CardRarity.Rare, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var enemies = play.Target!.CombatState!.Creatures.Where(c => c.IsEnemy && c.IsAlive);
        foreach (var e in enemies)
            await SinkingPower.Apply(ctx, e, layers: _sink, intensity: 1, base.Owner.Creature, this);
    }

    protected override void OnUpgrade() => _sink += SinkUp;
}