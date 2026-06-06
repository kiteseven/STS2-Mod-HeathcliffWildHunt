using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using GameCardPileCmd = MegaCrit.Sts2.Core.Commands.CardPileCmd;

namespace HeathcliffWildHuntMod.Compat;

/// <summary>
/// 双版本差异较大、又不便用全局别名（同名类多重载、参数语义改变）的命令，集中在此做显式包装。
/// 目前仅 <see cref="AddGeneratedCardToCombat"/>：
/// 测试版签名是 <c>(card, pile, Player? creator, position)</c>，正式版改成 <c>(card, pile, bool addedByPlayer, position)</c>。
/// 本 mod 4 处调用都是「玩家自己生成卡进手牌」，creator 非空 → 正式版统一传 <c>addedByPlayer: true</c>。
/// 业务侧把 <c>CardPileCmd.AddGeneratedCardToCombat(...)</c> 改成 <c>CompatCmd.AddGeneratedCardToCombat(...)</c> 即可，
/// 仍按测试版「传 Player」的旧心智书写，正式版下自动转成 bool。
/// </summary>
internal static class CompatCmd
{
    /// <summary>生成一张卡进战斗牌堆。<paramref name="creator"/> 非空表示由该玩家生成（正式版映射为 addedByPlayer:true）。</summary>
    public static Task<CardPileAddResult> AddGeneratedCardToCombat(
        CardModel card, PileType newPileType, Player? creator,
        CardPilePosition position = CardPilePosition.Bottom)
#if OFFICIAL
        => GameCardPileCmd.AddGeneratedCardToCombat(card, newPileType, creator != null, position);
#else
        => GameCardPileCmd.AddGeneratedCardToCombat(card, newPileType, creator, position);
#endif
}
