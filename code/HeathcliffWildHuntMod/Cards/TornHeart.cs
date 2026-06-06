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
/// <summary>U16 破碎的心：1费攻击5(7)×3，每次攻击扣目标2理智，抽2(3+)。注意：原设计是初始抽3升级抽2，但txt说原来的效果和升级的效果弄反了，所以改为初始抽2升级抽3。</summary>
public sealed class TornHeart : CardModel
{
    private const decimal BaseDamage = 5m;
    private const decimal UpgradeDamageBy = 2m;
    private const int DrainPerHit = 2;
    private const int DrawBase = 2;
    private const int DrawUp = 1;
    private int _draw = DrawBase;
    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Strike };
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(BaseDamage, ValueProp.Move) };
    public TornHeart() : base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy) { }
    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var t = play.Target!;
        for (int i = 0; i < 3; i++)
        {
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this).Targeting(t)
                .WithAttackerAnim(HeathcliffAttackAnim.PickFor(this), HeathcliffAttackAnim.PickHitDelayFor(this))
                .WithHitFx("vfx/vfx_attack_slash").Execute(ctx);
            await SanityPower.Drain(ctx, t, DrainPerHit, base.Owner.Creature, this);
        }
        await CardPileCmd.Draw(ctx, _draw, base.Owner);
    }
    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(UpgradeDamageBy);
        _draw += DrawUp;
    }
}