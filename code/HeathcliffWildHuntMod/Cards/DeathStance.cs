using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace HeathcliffWildHuntMod.Cards;

/// <summary>C16 藏队脚步：1费技能10(14)格挡，给全体敌人2(4+)易伤，抽1。</summary>
public sealed class DeathStance : CardModel
{
    private const decimal BaseBlock = 10m;
    private const decimal UpgradeBlockBy = 4m;
    private const int VulnBase = 2;
    private const int VulnUp = 2;
    private int _vuln = VulnBase;
    public override bool GainsBlock => true;
    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Defend };
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new BlockVar(BaseBlock, ValueProp.Move) };
    public DeathStance() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { }
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var o = base.Owner.Creature;
        await CreatureCmd.GainBlock(o, DynamicVars.Block, play);
        // 给全体敌人易伤
        var enemies = o.CombatState!.Creatures.Where(c => c.IsEnemy && c.IsAlive);
        foreach (var enemy in enemies)
            await PowerCmd.Apply<VulnerablePower>(ctx, enemy, _vuln, o, this);
        await CardPileCmd.Draw(ctx, 1, base.Owner);
    }
    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(UpgradeBlockBy);
        _vuln += VulnUp;
    }
}
