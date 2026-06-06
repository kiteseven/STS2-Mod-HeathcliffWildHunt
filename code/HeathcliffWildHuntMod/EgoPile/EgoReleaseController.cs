using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using HeathcliffWildHuntMod.Cards;

namespace HeathcliffWildHuntMod.EgoPile;

/// <summary>
/// EGO 主动释放控制器（框架核心）：展开选卡面板 → 调进手牌 → 由 vanilla 接管选目标/确认/取消。
/// </summary>
internal static class EgoReleaseController
{
    /// <summary>打开 EGO 选择面板并将选中的 EGO 卡调入手牌。</summary>
    public static async Task OpenAndReleaseAsync(Player player)
    {
        // owner 门禁：只有 EGO 框架的拥有者才能主动释放
        if (!EgoFramework.IsOwnerPlayer(player)) return;

        var combatState = player.PlayerCombatState;
        if (combatState is null) return;

        var egoPile = EgoPileStorage.PeekForCombatState(combatState);
        if (egoPile is null || egoPile.Cards.Count == 0) return;

        // 收集所有 EGO 卡
        var egoCards = egoPile.Cards.Where(EgoCardBase.IsEgo).ToList();
        if (egoCards.Count == 0) return;

        // 弹出选卡面板（selectCount=1 单选，RequireManualConfirmation=false 点击即确认）
        var prefs = new CardSelectorPrefs(
            prompt: new LocString("HEATHCLIFF_WILDHUNT_EGO_RELEASE_PROMPT"),
            selectCount: 1
        ) { RequireManualConfirmation = false };

        var ctx = new PlayerChoiceContext(combatState, player);
        var selected = await CardSelectCmd.FromSimpleGrid(ctx, egoCards, player, prefs);

        if (selected.Count == 0) return; // 玩家取消

        var chosen = selected[0];

        // 从 EGO 堆移除（skipVisuals 跳过动画，因为后续会调进手牌有动画）
        await CardPileCmd.RemoveFromCombat(chosen, skipVisuals: true);

        // 调进手牌顶部（vanilla 接管后续选目标/确认/取消交互）
        await CardPileCmd.AddGeneratedCardToCombat(chosen, PileType.Hand, player, CardPilePosition.Top);
    }
}
