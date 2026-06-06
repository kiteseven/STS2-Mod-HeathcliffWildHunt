using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HeathcliffWildHuntMod.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HeathcliffWildHuntMod.Cards;

/// <summary>C13 藏队脚步：0费技能，给全体敌人2(4)易伤。</summary>
public sealed class FuneralStep : CardModel
{
    private const int VulnBase = 2;
    private const int VulnUp = 2;
    private int _vuln = VulnBase;

    protected override HashSet<CardTag> CanonicalTags => new();
    public FuneralStep() : base(0, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var enemies = play.Target!.CombatState!.Creatures.Where(c => c.IsEnemy && c.IsAlive);
        foreach (var e in enemies)
            await PowerCmd.Apply<VulnerablePower>(ctx, e, _vuln, base.Owner.Creature, this);
    }

    protected override void OnUpgrade() => _vuln += VulnUp;
}