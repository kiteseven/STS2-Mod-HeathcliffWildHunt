using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HeathcliffWildHuntMod.Powers;
using HeathcliffWildHuntMod.Relics;
using HeathcliffWildHuntMod.Compat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HeathcliffWildHuntMod.Cards;

/// <summary>
/// R21 葬礼记忆：1费攻击18(24)。若杜拉罕+理智>15（或有 CleanAllCathy），则此卡变为悲叹。
/// 条件满足时卡牌发光。目标每3级沉沦强度+3伤害；每层棺+2伤害。
/// 给全体敌人8(12+)层沉沦+4强度，+1棺。
/// </summary>
public sealed class Requiem : CardModel
{
    private const decimal BaseDamage = 18m;
    private const decimal UpgradeDamageBy = 6m;
    private const int SinkIntensity = 4;
    private const int SinkLayersBase = 8;
    private const int SinkLayersUp = 4;
    private const int CoffinGain = 1;
    private const int BonusPer3Sink = 3;
    private const int BonusPerCoffin = 2;
    private int _sinkLayers = SinkLayersBase;

    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Strike };
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new DamageVar(BaseDamage, ValueProp.Move) };

    /// <summary>条件满足时发光。</summary>
    /// <summary>Condition for glow: canonical context always false (no Owner).</summary>
    protected override bool ShouldGlowGoldInternal => !base.IsMutable ? false : LamentConditionMet();

    protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[]
    {
        // ⚠️ CardModel.Owner getter 有 AssertMutable()，图鉴里是 canonical 实例 → 先检查 IsMutable
        new HoverTip(new LocString("cards", !base.IsMutable
            ? "REQUIEM.tip"
            : (base.Owner?.Relics.Any(r => r is CleanAllCathyRelic) == true
                ? "REQUIEM.tipErasured" : "REQUIEM.tip"))),
    };

    // 先古(Ancient)稀有度：不进普通战斗/商店/奖励池（vanilla CardFactory 显式排除 Ancient），
    // 只通过先古专属途径获得（如达弗事件给的 DustyTome 随机抽一张本角色 Ancient 卡）。
    // ⚠️ 本角色必须至少有一张 Ancient 卡，否则 DustyTome.SetupForPlayer 会因候选为空抛 NPE → 达弗事件卡死。
    public Requiem() : base(1, CardType.Attack, CardRarity.Ancient, TargetType.AnyEnemy) { }

    private bool LamentConditionMet()
    {
        // ⚠️ Owner getter 有 AssertMutable() —— IsMutable 为 false 时快速返回 false
        if (!base.IsMutable) return false;
        var o = base.Owner?.Creature;
        if (o == null) return false;
        if (!o.HasPower<DullahanPower>()) return false;
        if (base.Owner?.Relics.Any(r => r is CleanAllCathyRelic) == true)
            return true;
        var s = o.GetPower<SanityPower>();
        return s is not null && s.IsHigh;
    }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var o = base.Owner.Creature;
        var t = play.Target!;

        // 条件满足 → 替换为悲叹（由镇魂曲条件变化而来的悲叹本回合免费打出，不再二次扣能量）
        if (LamentConditionMet())
        {
            var lament = o.CombatState!.CreateCard(ModelDb.Card<Lament>(), base.Owner);
            lament.SetToFreeThisTurn();
            await CompatCmd.AddGeneratedCardToCombat(lament, PileType.Hand, base.Owner, CardPilePosition.Top);
            // 本卡打出时视为悲叹——不追加本卡效果，直接退出
            return;
        }

        // 正常结算
        var dmg = DynamicVars.Damage.BaseValue;
        var sinking = t.GetPower<SinkingPower>();
        if (sinking != null)
            dmg += (sinking.Intensity / 3) * BonusPer3Sink;
        dmg += o.GetPowerAmount<CoffinPower>() * BonusPerCoffin;

        await DamageCmd.Attack(dmg).FromCard(this).Targeting(t)
            .WithAttackerAnim(HeathcliffAttackAnim.Attack3, HeathcliffAttackAnim.Attack3HitDelay)
            .WithHitFx("vfx/vfx_attack_slash").Execute(ctx);

        // 给全体敌人施加沉沦
        var enemies = o.CombatState!.Creatures.Where(c => c.IsEnemy && c.IsAlive);
        foreach (var enemy in enemies)
            await SinkingPower.Apply(ctx, enemy, layers: _sinkLayers, intensity: SinkIntensity, o, this);
        await CoffinPower.Gain(ctx, o, CoffinGain, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(UpgradeDamageBy);
        _sinkLayers += SinkLayersUp;
    }
}
