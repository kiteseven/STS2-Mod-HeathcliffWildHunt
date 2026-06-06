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
/// <summary>R35 空棺爆裂：2费攻击，消耗至多5棺，每棺造成5(7)伤害，改为每层额外造成2次伤害。</summary>
public sealed class CoffinBurst : CardModel
{
    private const decimal DamagePerHit = 5m;
    private const decimal UpDamage = 2m;
    private const int MaxConsume = 5;
    private decimal _dmg = DamagePerHit;

    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Strike };
    protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(DamagePerHit, ValueProp.Move) };

    public CoffinBurst() : base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var o = base.Owner.Creature;
        var t = play.Target!;
        int consumed = await CoffinPower.Consume(ctx, o, MaxConsume, this);

        // 每层棺造成2次伤害（原来是1次 * 棺数层的伤害）
        for (int i = 0; i < consumed * 2; i++)
        {
            await DamageCmd.Attack(_dmg).FromCard(this).Targeting(t)
                .WithAttackerAnim(HeathcliffAttackAnim.PickFor(this), HeathcliffAttackAnim.PickHitDelayFor(this))
                .WithHitFx("vfx/vfx_attack_slash").Execute(ctx);
        }
    }

    protected override void OnUpgrade() => _dmg += UpDamage;
}