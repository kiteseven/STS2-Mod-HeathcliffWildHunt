using System.Linq;
using System.Threading.Tasks;
using HeathcliffWildHuntMod.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rooms;

namespace HeathcliffWildHuntMod.Relics;

/// <summary>
/// 凯瑟琳的棺材——愿她……在苦痛中醒来！开局获得 2 层棺。
/// </summary>
public sealed class CatherineCoffinRelic : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Starter;

    /// <summary>只有当玩家同时持有 CleanAllCathy 时才抹消文本。</summary>
    public override LocString Title => IsMutable && base.Owner != null && base.Owner.Relics.Any(r => r is CleanAllCathyRelic)
        ? new LocString("relics", "CATHERINE_COFFIN_RELIC.titleErasured")
        : new LocString("relics", "CATHERINE_COFFIN_RELIC.title");

    public override string PackedIconPath => "res://images/relics/catherine_coffin.png";
    protected override string PackedIconOutlinePath => "res://images/relics/outline/catherine_coffin.png";
    protected override string BigIconPath => "res://images/relics/large/catherine_coffin.png";

    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        if (base.Owner == null) return;
        await CoffinPower.Gain(new ThrowingPlayerChoiceContext(), base.Owner.Creature, 2, null);
    }
}
