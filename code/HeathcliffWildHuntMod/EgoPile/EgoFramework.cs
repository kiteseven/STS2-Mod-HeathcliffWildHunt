using System;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using HeathcliffWildHuntMod.Powers;

namespace HeathcliffWildHuntMod.EgoPile;

/// <summary>
/// EGO 系统的<b>可复用配置中枢</b>——这一整套 EGO/理智/侵蚀机制设计成「自包含源码模块」：
/// 整个文件夹连同 <see cref="EgoCardBase"/>/<see cref="EgoPileStorage"/>/<see cref="SanityPower"/>/
/// <see cref="ErosionPower"/> 及四个 Patch 一起拷进新角色 mod（如未来的辛克莱），把命名空间前缀
/// 全局替换，再在该 mod 的 bootstrap 里调一次 <see cref="Configure{TCharacter}"/> 即可直接复用，
/// <b>不引用任何前置库</b>（符合本工程「自己造轮子」铁律）。
///
/// <para>
/// <b>多 mod 共存（关键设计）：</b>用户会把希斯克利夫与辛克莱两个 EGO mod<b>同时</b>载入同一局游戏，
/// 二者必须互不干扰。一局游戏里玩家<b>有且仅有一个</b>角色，因此「只有当前出战角色 == 本 mod 拥有的角色时
/// 才动手」这一条 owner 门禁，就能把两套 EGO 系统彻底隔离。
/// </para>
///
/// <para>
/// 共存的两类真实冲突，及本框架的解法：
/// <list type="number">
///   <item><b>PileType 撞值</b>：两 mod 若都硬编码同一个自定义 PileType 值，
///         <c>CardPile.Get</c> 的两个 Prefix 会同时命中、争抢同一堆。
///         解法：<see cref="EgoPileType"/> 改为<b>每个 mod 各自传入</b>的唯一值（见 <see cref="Configure{TCharacter}"/>）。</item>
///   <item><b>理智重复附加</b>：战斗开始给所有 creature 挂 <see cref="SanityPower"/> 是无差别的，
///         两 mod 都跑就会给每个 creature 挂两条理智条（两 mod 的 SanityPower 是<b>不同类型</b>，
///         <c>HasPower&lt;SanityPower&gt;</c> 互不可见）。
///         解法：<see cref="AttachSanityIfOwnerRun"/> 仅在「本局出战角色 == owner」时才挂。</item>
/// </list>
/// 另外两个 Patch（建堆分流、AllPiles 拼接）<b>天然隔离</b>，无需 owner 门禁：
/// 前者靠 <c>EgoCardBase.IsEgo</c> 的<b>逐程序集类型标识</b>（只认本程序集的 EGO 卡），
/// 后者靠 <see cref="EgoPileStorage.PeekForCombatState"/>（只有本 mod 建过堆才非空）。
/// </para>
/// </summary>
internal static class EgoFramework
{
    /// <summary>
    /// 本 mod 拥有的出战角色类型（如 <c>Heathcliff</c>）。owner 门禁据此判定「本局是不是我的角色」。
    /// 未 <see cref="Configure{TCharacter}"/> 前为 null —— 此时所有门禁判否，框架按兵不动。
    /// </summary>
    private static Type? _ownerCharacterType;

    /// <summary>
    /// 本 mod 的 EGO 专属堆 <see cref="PileType"/> 值。<b>每个 mod 必须各传一个唯一值</b>，
    /// 否则共存时与别的 EGO mod 撞值。默认值仅作未配置时的兜底，正式使用务必经 <see cref="Configure{TCharacter}"/> 覆盖。
    /// </summary>
    public static PileType EgoPileType { get; private set; } = (PileType)0x4000_0E60;

    /// <summary>
    /// 一次性配置入口：记录本 mod 的出战角色类型 + EGO 堆唯一 PileType。
    /// 在 mod 的 bootstrap（Harmony patch 应用前后皆可）调用一次。
    /// </summary>
    /// <typeparam name="TCharacter">本 mod 的出战角色（如 <c>Heathcliff</c>）。owner 门禁的判定基准。</typeparam>
    /// <param name="egoPileType">本 mod EGO 专属堆的唯一 PileType 值（高值，避开 vanilla 0–6，且与其它 EGO mod 不同）。</param>
    public static void Configure<TCharacter>(PileType egoPileType)
        where TCharacter : CharacterModel
    {
        _ownerCharacterType = typeof(TCharacter);
        EgoPileType = egoPileType;
    }

    /// <summary>判定一个角色是否为本 mod 拥有的出战角色（owner 门禁核心）。未配置则恒 false。</summary>
    public static bool IsOwnerCharacter(CharacterModel? character)
        => _ownerCharacterType is not null && character is not null
           && _ownerCharacterType.IsInstanceOfType(character);

    /// <summary>判定一名玩家是否在操控本 mod 的出战角色。</summary>
    public static bool IsOwnerPlayer(Player? player)
        => player is not null && IsOwnerCharacter(player.Character);

    /// <summary>
    /// 主动释放 EGO 的<b>占位理智成本</b>（本版占位值，正式数值后续再定）。
    /// 集中放一处便于后续调数值 + 辛克莱复用。扣除发生在 EGO 卡 <c>OnPlay</c> 内——
    /// 保证只有「确认打出」才扣理智（与能量同步，取消选目标则不扣）。
    /// </summary>
    public const int ActiveReleaseSanityCost = 15;

    /// <summary>
    /// 把一张 EGO 卡搬回本玩家的 EGO 专属堆（取消选目标 / 打出后回收都走这里，保证主动释放可重复）。
    /// <para>
    /// EGO 堆是<b>非战斗堆</b>（<c>IsCombatPile</c>=false），故<b>不能</b>用
    /// <c>CardPileCmd.RemoveFromCombat</c>/<c>AddGeneratedCardToCombat</c>（二者都断言 combat pile，会抛异常）。
    /// 必须直接走底层 <c>CardPile.RemoveInternal</c>/<c>AddInternal</c> 手动搬运：
    /// 先从当前所在堆（Hand/Exhaust/...）摘除以清掉 <c>card.Pile</c> 引用，再塞回 EGO 专属堆。
    /// </para>
    /// </summary>
    public static void ReturnCardToEgoPile(Player? player, CardModel? card)
    {
        if (player is null || card is null) return;
        var egoPile = EgoPileStorage.Resolve(player);
        if (egoPile is null) return;
        // 已经在 EGO 堆里就不重复搬（避免 AddInternal 重复入堆）
        if (ReferenceEquals(card.Pile, egoPile)) return;
        // 先从当前堆摘除（silent：不触发抽/弃牌视觉），清空 card.Pile
        card.Pile?.RemoveInternal(card, silent: true);
        // 再塞回 EGO 专属堆
        egoPile.AddInternal(card, silent: true);
    }

    /// <summary>
    /// 战斗开始时给场上<b>所有</b> creature（玩家 + 敌人）挂一份 0 层 <see cref="SanityPower"/>，
    /// <b>但仅当本局出战角色 == owner 时</b>才执行——这正是共存时避免理智条重复附加的门禁。
    /// <para>
    /// 「0 层起步」用「Apply 1 后 ModifyAmount -1」落地：<c>PowerCmd.Apply(amount=0)</c> 会被当 no-op，
    /// 拿不到实例，故先 +1 让 power 落地再清零。
    /// </para>
    /// </summary>
    public static async Task AttachSanityIfOwnerRun(ICombatStateCompat state)
    {
        // 本局玩家不是 owner 角色 → 整个理智系统不参与（让真正拥有该角色的那个 mod 去挂）
        bool ownerRun = state.Creatures.Any(
            c => c is not null && c.IsPlayer && IsOwnerCharacter(c.Player?.Character));
        if (!ownerRun) return;

        // ChoiceContext：战斗刚开始没有决策上下文，传 null（vanilla 各 Power 钩子里 Apply 也允许 null）
        var ctx = (PlayerChoiceContext)null!;
        foreach (var creature in state.Creatures)
        {
            if (creature is null) continue;
            if (creature.HasPower<SanityPower>()) continue;

            await PowerCmd.Apply<SanityPower>(ctx, creature, 1, creature, null);
            var sanity = creature.GetPower<SanityPower>();
            if (sanity is not null)
                await PowerCmd.ModifyAmount(ctx, sanity, -1, creature, null);
        }
    }
}
