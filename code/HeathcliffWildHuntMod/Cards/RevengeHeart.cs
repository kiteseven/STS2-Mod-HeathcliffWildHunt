using System.Collections.Generic;
using System.Threading.Tasks;
using HeathcliffWildHuntMod.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HeathcliffWildHuntMod.Cards;

/// <summary>C22 被撕碎的心：1费(0费+)技能，给目标 7(8) 层 1(2) 强度沉沦。</summary>
public sealed class RevengeHeart : CardModel
{
    private const int LayersBase = 7;
    private const int LayersUpgrade = 1;
    private const int IntensityBase = 1;
    private const int IntensityUp = 1;
    private int _layers = LayersBase;
    private int _intensity = IntensityBase;

    protected override HashSet<CardTag> CanonicalTags => new();
    public RevengeHeart() : base(1, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var t = play.Target!;
        // 沉沦：加 _layers 层 + _intensity 强度（纯累加）
        await SinkingPower.Apply(ctx, t, layers: _layers, intensity: _intensity, base.Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        _layers += LayersUpgrade;
        _intensity += IntensityUp;
        EnergyCost.UpgradeBy(-1);
    }
}
