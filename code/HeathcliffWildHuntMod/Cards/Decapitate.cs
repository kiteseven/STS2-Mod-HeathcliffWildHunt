using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HeathcliffWildHuntMod.Cards;

/// <summary>
/// B1 斩首 Decapitate：1 费攻击。
/// 效果：造成 6 (9) 伤害。最基础的攻击卡，不携带任何资源 / debuff 操作。
/// 数值锚点：与 STS2 官方 StrikeIronclad 1费 6(9) 持平。
/// </summary>
public sealed class Decapitate : CardModel
{
    // 基础伤害（升级 +2）
    private const decimal BaseDamage = 5m;
    private const decimal UpgradeDamageBy = 2m;

    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Strike };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new DamageVar(BaseDamage, ValueProp.Move) };

    public Decapitate() : base(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        // 单段攻击：力量 / 易伤 / 减伤等加成对总额一次性结算
        // 攻击动画走 [[HeathcliffAttackAnim]] 统一选 trigger；delay 也来自 helper,让伤害落在命中帧而非动画一开始
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this).Targeting(play.Target)
            .WithAttackerAnim(HeathcliffAttackAnim.PickFor(this), HeathcliffAttackAnim.PickHitDelayFor(this))
            .WithHitFx("vfx/vfx_attack_slash")
            .Execute(ctx);
    }

    /// <summary>升级：伤害 +3。</summary>
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(UpgradeDamageBy);
}
