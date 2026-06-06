using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HeathcliffWildHuntMod.Powers;

/// <summary>
/// 沸腾怨念：每次获得棺时层数+1。回合开始按层数阈值给力量/敏捷，然后重置层数。
/// <list type="bullet">
///   <item>每3层 → +2 力量（最多6）</item>
///   <item>每4层 → +1 敏捷（最多2）</item>
/// </list>
/// </summary>
public sealed class NightmareAvengerReturnsPower : PowerModel
{
    // 已累计发放的力量/敏捷（防跨回合溢出上限）
    private int _giftedStrength;
    private int _giftedDexterity;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    /// <summary>侦测棺增加→层数+1。</summary>
#if OFFICIAL
    // 正式版去掉首参 PlayerChoiceContext，补一个 null 局部变量保持方法体不变（ctx 仅流向 PowerCmd shim）
    public override async Task AfterPowerAmountChanged(
        PowerModel power, decimal amount,
        Creature? applier, CardModel? cardSource)
    {
        PlayerChoiceContext choiceContext = null!;
#else
    public override async Task AfterPowerAmountChanged(
        PlayerChoiceContext choiceContext, PowerModel power, decimal amount,
        Creature? applier, CardModel? cardSource)
    {
#endif
        if (power is not CoffinPower) return;
        if (power.Owner != base.Owner) return;
        if (amount > 0)
            await PowerCmd.ModifyAmount(choiceContext, this, (int)amount, applier, cardSource);
    }

    /// <summary>回合开始：按阈值发力量/敏捷，然后重置层数。</summary>
    public override async Task AfterPlayerTurnStartEarly(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != base.Owner.Player) return;
        var amt = base.Amount;
        if (amt <= 0) return;

        // 每3层+2力量（上限6）→ 上限等价于最多触发3次(amt>=9)
        int wantStr = System.Math.Min(6, (amt / 3) * 2);
        // 每4层+1敏捷（上限2）→ 上限等价于最多触发2次(amt>=8)
        int wantDex = System.Math.Min(2, amt / 4);

        int deltaStr = wantStr - _giftedStrength;
        if (deltaStr != 0)
        {
            await PowerCmd.Apply<StrengthPower>(choiceContext, base.Owner, deltaStr, base.Owner, null);
            _giftedStrength = wantStr;
        }
        int deltaDex = wantDex - _giftedDexterity;
        if (deltaDex != 0)
        {
            await PowerCmd.Apply<DexterityPower>(choiceContext, base.Owner, deltaDex, base.Owner, null);
            _giftedDexterity = wantDex;
        }

        // 发完归零，本回合重新累计
        await PowerCmd.ModifyAmount(choiceContext, this, -amt, base.Owner, null);
    }
}
