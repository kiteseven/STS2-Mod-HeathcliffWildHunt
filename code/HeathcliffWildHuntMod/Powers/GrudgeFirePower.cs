using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
namespace HeathcliffWildHuntMod.Powers;
public sealed class GrudgeFirePower : PowerModel
{
    private int _gainedThisTurn;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.None;
    public override async Task AfterDamageReceived(PlayerChoiceContext ctx, Creature target, MegaCrit.Sts2.Core.Entities.Creatures.DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != base.Owner || !props.HasFlag(ValueProp.Move) || result.UnblockedDamage <= 0) return;
        if (_gainedThisTurn >= 5) return;
        _gainedThisTurn++;
        await CoffinPower.Gain(ctx, base.Owner, 1, cardSource);
    }
    public override Task AfterPlayerTurnStartEarly(PlayerChoiceContext ctx, MegaCrit.Sts2.Core.Entities.Players.Player player)
    {
        if (player == base.Owner.Player) _gainedThisTurn = 0;
        return Task.CompletedTask;
    }
}
