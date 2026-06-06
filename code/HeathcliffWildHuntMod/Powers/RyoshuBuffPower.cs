using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace HeathcliffWildHuntMod.Powers;

/// <summary>良秀随从被动：存活期间，召唤时狂猎 +1 敏捷；狂猎每次攻击获得 1 格挡。随从死亡时移除敏捷加成。</summary>
public sealed class RyoshuBuffPower : PowerModel
{
    private Creature? _player;
    private int _appliedDexterity;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.None;

    public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        // 随从是怪物型 Creature，Player 永远为 null；必须用 PetOwner 追溯到所属玩家
        _player = base.Owner.PetOwner?.Creature;
        if (_player != null)
        {
            _appliedDexterity = 1;
            await PowerCmd.Apply<DexterityPower>(new ThrowingPlayerChoiceContext(), _player, _appliedDexterity, _player, cardSource);
        }
    }

    /// <summary>狂猎每次攻击命中获得 1 点格挡</summary>
    public override async Task AfterDamageGiven(PlayerChoiceContext ctx, Creature? dealer, DamageResult result,
        ValueProp props, Creature target, CardModel? cardSource)
    {
        if (_player == null || dealer != _player || !props.HasFlag(ValueProp.Move)) return;
        await CreatureCmd.GainBlock(_player, 1m, ValueProp.Move, null);
    }

    /// <summary>随从死亡 → 移除授予的敏捷</summary>
    public override async Task AfterRemoved(Creature oldOwner)
    {
        if (_player != null && _appliedDexterity > 0)
        {
            await PowerCmd.Apply<DexterityPower>(new ThrowingPlayerChoiceContext(), _player, -_appliedDexterity, _player, null);
            _appliedDexterity = 0;
        }
    }
}
