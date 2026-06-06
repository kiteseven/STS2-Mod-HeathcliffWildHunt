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
/// <summary>C14 葬礼记忆：0费技能，给全体敌人8(12)层1强度沉沦，消耗。</summary>
public sealed class FuneralMemory : CardModel
{
    private const int SinkBase = 8;
    private const int SinkUp = 4;
    private int _sink = SinkBase;
    protected override HashSet<CardTag> CanonicalTags => new();
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
    public FuneralMemory() : base(0, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy) { }
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var enemies = play.Target!.CombatState!.Creatures.Where(c => c.IsEnemy && c.IsAlive);
        foreach (var e in enemies)
            // 各加 _sink 层 + 1 强度（统一累加入口）
            await SinkingPower.Apply(ctx, e, layers: _sink, intensity: 1, base.Owner.Creature, this);
    }
    protected override void OnUpgrade() => _sink += SinkUp;
}