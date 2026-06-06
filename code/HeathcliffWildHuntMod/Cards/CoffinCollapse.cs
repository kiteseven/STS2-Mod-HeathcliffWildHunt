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
public sealed class CoffinCollapse : CardModel
{
    private const decimal PerCoffin = 3m; private const decimal UpPer = 2m;
    private decimal _per = PerCoffin;
    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Strike };
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(0m, ValueProp.Move) };
    public CoffinCollapse() : base(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var o = base.Owner.Creature; var t = play.Target!;
        int coffin = o.GetPowerAmount<CoffinPower>();
        if (coffin > 0) { await CoffinPower.Consume(ctx, o, coffin, this); }
        if (coffin > 0) await DamageCmd.Attack(coffin * _per).FromCard(this).Targeting(t)
            .WithAttackerAnim(HeathcliffAttackAnim.PickFor(this), HeathcliffAttackAnim.PickHitDelayFor(this))
            .WithHitFx("vfx/vfx_attack_slash").Execute(ctx);
    }
    protected override void OnUpgrade() => _per += UpPer;
}