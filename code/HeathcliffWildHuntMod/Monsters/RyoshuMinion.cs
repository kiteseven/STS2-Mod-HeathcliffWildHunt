using System.Threading.Tasks;
using HeathcliffWildHuntMod.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
namespace HeathcliffWildHuntMod.Monsters;
public sealed class RyoshuMinion : WildHuntMinionModel
{
    public override int MinInitialHp => 14;
    public override int MaxInitialHp => 14;
    public override async Task OnSummon(Player owner, Creature self, CardModel? source)
    {
        if (IsUpgraded) await CreatureCmd.SetMaxAndCurrentHp(self, 18);
        // 随从存活时有效——buff 挂在随从身上，随从死亡自动消失
        await PowerCmd.Apply<RyoshuBuffPower>(new ThrowingPlayerChoiceContext(), self, 1, owner.Creature, source);
        var state = owner.Creature.GetPower<HeathcliffStatePower>();
        if (state != null) state.SummonsEverSummonedThisCombat++;
    }
}
