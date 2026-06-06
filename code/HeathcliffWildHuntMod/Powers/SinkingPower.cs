using System.Threading.Tasks;
using HeathcliffWildHuntMod.Combat.Ui;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HeathcliffWildHuntMod.Powers;

/// <summary>
/// 沉沦 Sinking（敌方 debuff）。
/// 双参数：层数 (Amount) + 强度 (Intensity)。
/// 玩家每次对持有此 debuff 的目标造成伤害时：
///   1) 目标 SanityPower 减 Intensity；
///   2) 沉沦层数 -1；
///   3) 若目标理智已触底 -45，则改为造成 Intensity 点忧郁伤害（无视格挡）。
///
/// 施加规则：统一走 <see cref="Apply"/>（加层数 + <b>累加</b>强度）。强度纯累加——多次施加会叠加，
/// 不再有"自动补 1 强度"或"补齐到目标值"的旧逻辑（那是强度不累加 bug 的根源）。层数与强度上限均为 99。
///
/// 图标显示：强度与层数<b>一起</b>放在沉沦图标右边的主计数位（<see cref="IPowerMainAmountTextProvider"/>），
/// 格式「<b>强度 层数</b>」（强度在前、层数在后，空格分隔）。强度为 0 时仅显示层数。
/// </summary>
public sealed class SinkingPower : PowerModel, IPowerMainAmountTextProvider
{
    private class Data
    {
        public int Intensity;
    }

    /// <summary>层数（Amount）上限。</summary>
    public const int MaxStacks = 99;

    /// <summary>强度（Intensity）上限。</summary>
    public const int MaxIntensity = 99;

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>
    /// DisplayAmount 仍返回层数（供 vanilla 内部逻辑/数值比较用）；
    /// 实际图标文本由 <see cref="GetMainAmountText"/> 改写成「强度 层数」。
    /// </summary>
    public override int DisplayAmount => Amount;

    protected override object InitInternalData() => new Data();

    /// <summary>
    /// 统一施加沉沦入口：一次性「加层数 + 累加强度」。
    /// <para>
    /// 强度走<b>纯累加</b>语义——本次施加的 <paramref name="intensity"/> 直接叠加到现有强度上，
    /// 而不是「补齐到某个目标值」。多张沉沦卡先后施加时强度会累积（例：先 +1 再 +4 → 总强度 5）。
    /// </para>
    /// <para>
    /// 注：层数由 <see cref="PowerCmd"/> 的 stacking 机制累加；强度不经 SetAmount，需在拿到实例后显式累加。
    /// 首次施加（目标原本无沉沦）时，新实例由 PowerCmd 创建，本方法在其后用 <see cref="GetPower{T}"/> 取回再累加强度。
    /// </para>
    /// </summary>
    /// <param name="layers">本次施加的层数（≥1）。</param>
    /// <param name="intensity">本次叠加的强度（≥1）。</param>
    public static async Task Apply(PlayerChoiceContext ctx, Creature target, int layers, int intensity,
        Creature? applier, CardModel? source)
    {
        if (layers > 0)
        {
            await PowerCmd.Apply<SinkingPower>(ctx, target, layers, applier, source);
            // 层数封顶 99：PowerCmd 的 stacking 不带上限，叠加后若超 99 直接截回。
            var power = target.GetPower<SinkingPower>();
            if (power is not null && power.Amount > MaxStacks)
                power.SetAmount(MaxStacks);
        }
        if (intensity > 0)
            target.GetPower<SinkingPower>()?.ApplyIntensity(intensity);
    }

    public int Intensity => GetInternalData<Data>().Intensity;

    public void ApplyIntensity(int amount)
    {
        AssertMutable();
        var data = GetInternalData<Data>();
        data.Intensity += amount;
        if (data.Intensity < 0) data.Intensity = 0;
        if (data.Intensity > MaxIntensity) data.Intensity = MaxIntensity; // 强度封顶 99
        // 强度走 internal data，不像层数那样经 SetAmount 自动触发 DisplayAmountChanged。
        // 主动通知一次，让能力图标（含层数角标）随强度变化重绘。
        InvokeDisplayAmountChanged();
    }

    /// <summary>
    /// 主计数文本：强度与层数一起显示在图标右边，格式「强度 层数」（强度在前、层数在后）。
    /// 强度为 0 时仅显示层数（退化为单值）。主计数 label 右对齐，故空格分隔后强度在左、层数在右。
    /// </summary>
    public string? GetMainAmountText()
    {
        int stacks = Amount;
        int intensity = Intensity;
        return intensity > 0 ? $"{intensity} {stacks}" : stacks.ToString();
    }

    public override async Task AfterDamageGiven(
        PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result,
        ValueProp props, Creature target, CardModel? cardSource)
    {
        // 仅响应玩家方造成的伤害；dealer 为空 / 非玩家方 / 非 Move 类伤害都跳过
        if (dealer is null || !dealer.IsPlayer) return;
        if (target != base.Owner) return;
        if (base.Amount <= 0) return;
        if (!props.HasFlag(ValueProp.Move)) return;

        int intensity = Intensity;
        if (intensity <= 0) return;

        var sanity = target.GetPower<SanityPower>();
        bool atBottom = sanity is not null && sanity.Amount <= SanityPower.MinSanity;

        if (atBottom)
        {
            await CreatureCmd.Damage(choiceContext, target, intensity,
                ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.SkipHurtAnim,
                base.Owner, cardSource);
        }
        else if (sanity is not null)
        {
            await SanityPower.Drain(choiceContext, target, intensity, base.Owner, cardSource);
        }

        await PowerCmd.ModifyAmount(choiceContext, this, -1m, base.Owner, cardSource);
    }
}
