using System.Collections.Generic;
using System.Threading.Tasks;
using HeathcliffWildHuntMod.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
namespace HeathcliffWildHuntMod.Cards;
/// <summary>C8 悲伤的低语：0费技能，给目标 1层1强度(2层2强度+) 沉沦 + 抽 1(2+)。</summary>
public sealed class SadWhisper : CardModel
{
    private const int LayersBase = 1;
    private const int LayersUp = 1;
    private const int IntensityBase = 1;
    private const int IntensityUp = 1;
    private const int DrawBase = 1;
    private const int DrawUp = 1;
    private int _layers = LayersBase;
    private int _intensity = IntensityBase;
    private int _draw = DrawBase;
    protected override HashSet<CardTag> CanonicalTags => new();
    public SadWhisper() : base(0, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy) { }
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var t = play.Target!;
        await SinkingPower.Apply(ctx, t, layers: _layers, intensity: _intensity, base.Owner.Creature, this);
        await CardPileCmd.Draw(ctx, _draw, base.Owner);
    }
    protected override void OnUpgrade()
    {
        _layers += LayersUp;
        _intensity += IntensityUp;
        _draw += DrawUp;
    }
}
