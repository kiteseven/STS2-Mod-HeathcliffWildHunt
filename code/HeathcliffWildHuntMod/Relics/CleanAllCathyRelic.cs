using System.Threading.Tasks;
using HeathcliffWildHuntMod.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rooms;

namespace HeathcliffWildHuntMod.Relics;

/// <summary>
/// 删除凯茜——Ancient 遗物，由 TouchOfOrobas 替换凯瑟琳的棺材而来。
/// </summary>
public sealed class CleanAllCathyRelic : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;

    public override LocString Title => IsMutable
        ? new LocString("relics", "CLEAN_ALL_CATHY_RELIC.titleErasured")
        : new LocString("relics", "CLEAN_ALL_CATHY_RELIC.title");

    public override string PackedIconPath => "res://images/relics/clean_all_cathy.png";
    protected override string PackedIconOutlinePath => "res://images/relics/outline/clean_all_cathy.png";
    protected override string BigIconPath => "res://images/relics/large/clean_all_cathy.png";

    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        if (base.Owner == null) return;
        var ctx = new ThrowingPlayerChoiceContext();
        await CoffinPower.Gain(ctx, base.Owner.Creature, 3, null);
        await PowerCmd.Apply<DullahanPower>(ctx, base.Owner.Creature, 1, base.Owner.Creature, null);
    }

#if OFFICIAL
    // 正式版去掉首参 PlayerChoiceContext，补一个 null 局部变量保持方法体不变（ctx 仅流向 PowerCmd shim）
    public override async Task AfterPowerAmountChanged(
        PowerModel power, decimal amount,
        Creature? applier, CardModel? cardSource)
    {
        PlayerChoiceContext choiceContext = null!;
#else
    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext, PowerModel power, decimal amount,
        Creature? applier, CardModel? cardSource)
    {
#endif
        if (power is not SanityPower || power.Owner != base.Owner.Creature) return;
        if (power.Amount > 0)
            await PowerCmd.ModifyAmount(choiceContext, power, -power.Amount, base.Owner.Creature, cardSource);
    }
}
