using System.Linq;
using System.Threading.Tasks;
using HeathcliffWildHuntMod.Monsters;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace HeathcliffWildHuntMod.Powers;

/// <summary>U29 哀悼之王 Power：每当有单位死亡时（包括队友和狂猎随从）+2力量+2棺。</summary>
public sealed class MourningKingPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.None;

    public override async Task AfterDamageGiven(PlayerChoiceContext ctx, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
    {
        // 任何单位死亡都触发（敌人、玩家队友、狂猎随从）
        if (!result.WasTargetKilled) return;

        await PowerCmd.Apply<StrengthPower>(ctx, base.Owner, 2, base.Owner, cardSource);
        await CoffinPower.Gain(ctx, base.Owner, 2, cardSource);
    }
}
