using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;

namespace HeathcliffWildHuntMod.EgoPile;

/// <summary>
/// EGO 专属堆的运行时存储。每个 <see cref="PlayerCombatState"/> 懒创建一个
/// <see cref="CardPile"/>(<see cref="EgoFramework.EgoPileType"/>) 实例。
/// <para>
/// 用 <see cref="ConditionalWeakTable{TKey,TValue}"/> 以 PlayerCombatState 为 key 存——
/// 战斗结束 PlayerCombatState 被丢弃后，对应 EGO 堆随之被 GC 回收，等价于「combat-only」生命周期。
/// （EGO 卡本体的持久化不靠这个堆，而是靠 EGO 卡常驻 <c>Player.Deck</c> 随 vanilla 存档走；
/// 本堆只是战斗中承载 EGO 卡的容器。详见 docs/plan-ego-system.md。）
/// </para>
/// </summary>
internal static class EgoPileStorage
{
    // key = PlayerCombatState（战斗态），value = 该战斗的 EGO 堆。state 被回收时条目自动消失。
    private static readonly ConditionalWeakTable<PlayerCombatState, CardPile> Piles = new();

    /// <summary>取（或懒创建）指定战斗态的 EGO 堆。</summary>
    public static CardPile GetForCombatState(PlayerCombatState state)
    {
        return Piles.GetValue(state, _ => new CardPile(EgoFramework.EgoPileType));
    }

    /// <summary>
    /// 按 <see cref="CardPile.Get"/> 的语义解析：玩家在战斗中则返回其 EGO 堆，否则 null（脱战无 combat pile）。
    /// </summary>
    public static CardPile? Resolve(Player player)
    {
        var state = player.PlayerCombatState;
        return state is null ? null : GetForCombatState(state);
    }

    /// <summary>若该战斗态已创建过 EGO 堆则返回，否则 null（不触发创建，供只读枚举用）。</summary>
    public static CardPile? PeekForCombatState(PlayerCombatState state)
    {
        return Piles.TryGetValue(state, out var pile) ? pile : null;
    }
}
