using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HeathcliffWildHuntMod.Powers;

/// <summary>奥提斯随从被动：存活期间，狂猎 +2 力量。随从死亡时移除此加成。</summary>
public sealed class OutisBuffPower : PowerModel
{
    private Creature? _player;
    private int _appliedStrength;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.None;

    public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        // 随从是怪物型 Creature，Player 永远为 null；必须用 PetOwner 追溯到所属玩家
        _player = base.Owner.PetOwner?.Creature;
        if (_player != null)
        {
            _appliedStrength = 2;
            await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), _player, _appliedStrength, _player, cardSource);
        }
    }

    /// <summary>随从死亡 → 移除授予的力量</summary>
    public override async Task AfterRemoved(Creature oldOwner)
    {
        if (_player != null && _appliedStrength > 0)
        {
            await PowerCmd.Apply<StrengthPower>(new ThrowingPlayerChoiceContext(), _player, -_appliedStrength, _player, null);
            _appliedStrength = 0;
        }
    }
}
