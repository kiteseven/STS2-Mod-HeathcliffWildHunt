using System.Collections.Generic;
using System.Threading.Tasks;
using HeathcliffWildHuntMod.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HeathcliffWildHuntMod.Cards;

/// <summary>
/// EGO「电线杆 Telepole」：1费技能，释放占位扣 20 理智。
/// <para>
/// 效果：获得 7 能量；自身获得 2 脆弱（爆能代价）。
/// </para>
/// </summary>
public sealed class Telepole : EgoCardBase
{
    private const int EnergyGain = 7;
    private const int FrailGain = 2;

    protected override HashSet<CardTag> CanonicalTags => new();
    public override IEnumerable<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust, CardKeyword.Ethereal };

    public Telepole() : base(1, CardType.Skill, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        var o = base.Owner.Creature;
        await SanityPower.Drain(ctx, o, 20, o, this);
        await PlayerCmd.GainEnergy(EnergyGain, base.Owner);
        // 自身获得 2 脆弱（爆发换来的破绽）
        await PowerCmd.Apply<FrailPower>(ctx, o, FrailGain, o, this);
    }
}
