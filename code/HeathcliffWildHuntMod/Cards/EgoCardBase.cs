using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace HeathcliffWildHuntMod.Cards;

/// <summary>
/// EGO 卡基类：所有 EGO 卡继承本类，统一获得「先古卡框 + EGO 判定标记」两个特征。
/// <para>
/// 设计要点：
/// 1. 构造强制 <see cref="CardRarity.Ancient"/> —— vanilla <c>NCard</c> 在 Ancient 稀有度下
///    自动切到先古卡视觉（ancient border / banner / portrait），无需自制卡框（参考 Resolve/Requiem）。
/// 2. <see cref="IsEgo"/> 是全局唯一的 EGO 判定入口；[[SanityPower]] / [[ErosionPower]] 都调它，
///    避免散落多份判定逻辑。
/// </para>
/// <para>
/// EGO 卡独立于普通卡池：不进 <c>HeathcliffCardPool.GenerateAllCards()</c>。它存在于 <c>Player.Deck</c>
/// （随 vanilla 存档持久化），战斗建堆时由 <c>PatchPopulateCombatStateRouteEgo</c> 分流到 EGO 专属堆，
/// 不进抽牌循环。详见 docs/plan-ego-system.md。
/// </para>
/// </summary>
public abstract class EgoCardBase : CardModel
{
    /// <summary>
    /// 子类构造转发：cost / type / target 由具体 EGO 卡决定，稀有度一律强制为 Ancient（拿先古框）。
    /// </summary>
    protected EgoCardBase(int cost, CardType type, TargetType target)
        : base(cost, type, CardRarity.Ancient, target)
    {
    }

    /// <summary>全局唯一 EGO 判定入口。判定一张卡是否为 EGO 卡（用于侵蚀塞牌 / 首张必打硬拦截等）。</summary>
    public static bool IsEgo(CardModel card) => card is EgoCardBase;
}
