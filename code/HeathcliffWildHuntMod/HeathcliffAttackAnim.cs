using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace HeathcliffWildHuntMod;

/// <summary>
/// 帮卡牌按稀有度 / 设计意图选合适的攻击动画 trigger（基础 / 罕见 / 稀有 + 特质）。
/// <para>
/// 设计规则（与设计案 v2 同步）：
/// <list type="bullet">
///   <item>Basic（基础）→ <c>Attack</c>（attack cue, 7 帧, 默认大剑挥砍）</item>
///   <item>Common（普通）/ Uncommon（罕见）→ <c>Attack2</c>（attack2 cue, 7 帧, 重连段）</item>
///   <item>Rare（稀有）/ Boss EGO（稀有度 BossEgo）→ <c>Attack3</c>（attack3 cue, 16 帧, 大段连斩）</item>
///   <item>特质卡（在 <see cref="UsesAttack3Anim"/> 白名单里）→ 强制 <c>Attack3</c>。
///        当前白名单：<see cref="HeathcliffWildHuntMod.Cards.IntoThisCoffin"/> 单独走 attack3。
///        未来"悲叹"/"哀恸"/"破灭吧"三联同名加进来即可。</item>
/// </list>
/// </para>
/// <para>
/// 用法：在 <c>OnPlay</c> 里把
/// <code>
///     .WithAttackerAnim(HeathcliffAttackAnim.PickFor(this), base.Owner.Character.AttackAnimDelay)
/// </code>
/// 加到 <see cref="MegaCrit.Sts2.Core.Commands.Builders.AttackCommand"/> 链上即可。
/// </para>
/// </summary>
internal static class HeathcliffAttackAnim
{
    /// <summary>vanilla 通用 attack trigger 名（被本工程的 cue dispatcher 映射到 attack cue）。</summary>
    public const string Attack = "Attack";
    /// <summary>罕见连段 trigger（映射到 attack2 cue）。</summary>
    public const string Attack2 = "Attack2";
    /// <summary>稀有大段连斩 trigger（映射到 attack3 cue）。</summary>
    public const string Attack3 = "Attack3";

    // ── 命中帧延迟（伤害结算落在哪一帧）────
    // 数值 = 该帧之前所有帧时长累加（参考 [[HeathcliffVisualCues]] 节奏常量 WindUp/Swing/HitHold/Recover）。
    // 让伤害贴合视觉命中点,而不是动画一开始就掉血(vanilla 默认 0.18 太早)。

    /// <summary>attack(7 帧)命中帧 skill1_5 的开始时间 = 0.14+0.14+0.10+0.10 = 0.48s。</summary>
    public const float AttackHitDelay = 0.48f;
    /// <summary>attack2(7 帧)命中帧 skill2_6 的开始时间 = 0.22+0.22+0.16+0.16+0.14 = 0.90s。</summary>
    public const float Attack2HitDelay = 0.90f;
    /// <summary>attack3(9 帧)命中帧 skill3_9 的开始时间。</summary>
    public const float Attack3HitDelay = 1.56f;

    /// <summary>
    /// 命名白名单：这些卡不论稀有度都走 attack3。当前条目：
    ///   - IntoThisCoffin（B5 把你也装进这棺材）
    ///   - 后续：悲叹 / 哀恸 / 破灭吧 三联（卡类加上后把类名加进来）
    /// </summary>
    private static readonly System.Collections.Generic.HashSet<string> UsesAttack3Anim = new()
    {
        nameof(HeathcliffWildHuntMod.Cards.IntoThisCoffin),
        // "Lament", "Grief", "BeShattered",  // 悲叹 / 哀恸 / 破灭吧 三联——加入卡类后取消注释
    };

    /// <summary>按卡决定要用哪个 attack trigger。</summary>
    public static string PickFor(CardModel card)
    {
        if (card == null) return Attack;
        // 1) 名字白名单优先（特质卡）
        if (UsesAttack3Anim.Contains(card.GetType().Name)) return Attack3;
        // 2) 按稀有度自动分流。EGO/Boss EGO 在 vanilla CardRarity 里没有专属枚举值，
        //    本工程后续若加 EGO 卡用 CardRarity.Rare 或 Ancient 标记，分到 attack3。
        return card.Rarity switch
        {
            CardRarity.Common or CardRarity.Uncommon  => Attack2,
            CardRarity.Rare or CardRarity.Ancient     => Attack3,
            _ /* Basic / None / Status / Curse / 其它 */ => Attack,
        };
    }

    /// <summary>
    /// 配套：按卡返回伤害结算应等待的秒数（让 vanilla 的 <c>WithAttackerAnim(name, delay)</c> 把伤害落在命中帧）。
    /// 与 <see cref="PickFor"/> 一一对应：basic→AttackHitDelay; common/uncommon→Attack2HitDelay; rare/特质卡→Attack3HitDelay。
    /// </summary>
    public static float PickHitDelayFor(CardModel card)
    {
        return PickFor(card) switch
        {
            Attack2 => Attack2HitDelay,
            Attack3 => Attack3HitDelay,
            _       => AttackHitDelay,
        };
    }
}
