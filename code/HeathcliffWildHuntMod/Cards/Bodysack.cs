using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using HeathcliffWildHuntMod.Powers;

namespace HeathcliffWildHuntMod.Cards;

/// <summary>
/// 初始 EGO「裹尸袋 Bodysack」：2费攻击40(54)，自身+2力量+1敏捷。消耗。
/// <para>
/// 角色开局自带的唯一 EGO（继承 <see cref="EgoCardBase"/> → 先古卡框 + EGO 判定）。
/// 常驻 Deck 随存档持久化，战斗建堆时被分流到 EGO 专属堆，理智触底进侵蚀后由侵蚀塞进手牌。
/// </para>
/// </summary>
public sealed class Bodysack : EgoCardBase
{
    private const decimal BaseDamage = 20m;
    private const decimal UpgradeDamageBy = 8m;

    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Strike };
    // Ethereal(虚无)：主动释放调进手牌后若回合末未打出，由 vanilla 回合末逻辑自动消耗(烧掉)，
    // 取代旧的「回合末 sweep 搬回 EGO 堆」方案（后者会卡住回合切换）。Exhaust：打出后也进消耗堆。
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust, CardKeyword.Ethereal };
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new DamageVar(BaseDamage, ValueProp.Move) };

    // 稀有度由 EgoCardBase 强制为 Ancient（先古框）；这里只传 cost / type / target。
    public Bodysack() : base(2, CardType.Attack, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var o = base.Owner.Creature;
        // 主动释放 EGO 的占位理智成本：放在 OnPlay 开头扣，确保「确认打出」才扣理智
        // （与能量同步——取消选目标则不入队 PlayCardAction，二者都不扣）。占位值集中在 EgoFramework。
        await SanityPower.Drain(ctx, o, EgoPile.EgoFramework.ActiveReleaseSanityCost, o, this);
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this).Targeting(play.Target!)
            .WithAttackerAnim(HeathcliffAttackAnim.Attack3, HeathcliffAttackAnim.Attack3HitDelay)
            .WithHitFx("vfx/vfx_attack_slash").Execute(ctx);
        await PowerCmd.Apply<StrengthPower>(ctx, o, 2, o, this);
        await PowerCmd.Apply<DexterityPower>(ctx, o, 1, o, this);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(UpgradeDamageBy);
}
