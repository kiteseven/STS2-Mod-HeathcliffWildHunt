using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HeathcliffWildHuntMod.Powers;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace HeathcliffWildHuntMod.Cards;

/// <summary>
/// 破碎的心 Broken Heart（类名沿用 PyreOffering）：1 费技能。消耗手牌中 1 张，获得 2(3) [gold]棺[/gold] 并恢复 6(8) [gold]理智[/gold]。
/// <para>
/// 烧牌手段（消耗方向）样例之一：把"主动消耗一张手牌"转化为狂猎核心资源（棺 + 理智），
/// 体现"自我献祭→换取力量"主题。无可消耗手牌时仍给资源（保底不废牌），但鼓励主动喂牌。
/// </para>
/// </summary>
public sealed class PyreOffering : CardModel
{
    private const int CoffinGainBase = 2;
    private const int CoffinGainUp = 1;
    private const int SanityRestore = 6;
    private const int SanityRestoreUp = 2;

    private int _coffinGain = CoffinGainBase;
    private int _sanityRestore = SanityRestore;

    protected override HashSet<CardTag> CanonicalTags => new();

    public PyreOffering() : base(1, CardType.Skill, CardRarity.Common, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var owner = base.Owner.Creature;

        // 选 1 张手牌消耗（本卡已离开手牌，不会选到自己）。无手牌可选则跳过消耗，仍给资源保底。
        var hand = PileType.Hand.GetPile(base.Owner);
        if (hand.Cards.Count > 0)
        {
            var picked = (await CardSelectCmd.FromHand(
                ctx, base.Owner,
                new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, 1),
                filter: null, source: this)).FirstOrDefault();
            if (picked != null)
                await CardCmd.Exhaust(ctx, picked);
        }

        await CoffinPower.Gain(ctx, owner, _coffinGain, this);
        await SanityPower.Restore(ctx, owner, _sanityRestore, owner, this);
    }

    protected override void OnUpgrade()
    {
        _coffinGain += CoffinGainUp;
        _sanityRestore += SanityRestoreUp;
    }
}
