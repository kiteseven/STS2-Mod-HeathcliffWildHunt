using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;

namespace HeathcliffWildHuntMod;

/// <summary>
/// 把 Heathcliff 注入卡牌图鉴。
/// 核心思路：不依赖 c.Pool / c.GetType() 等任何 per-card getter，
/// 而是预先算出狂猎卡池所有卡的 ModelId HashSet，filter 时直接查表。
/// </summary>

// ── 预计算的狂猎卡 ID 表 ──
internal static class HeathcliffCardIdSet
{
    internal static readonly HashSet<ModelId> Ids;

    static HeathcliffCardIdSet()
    {
        try
        {
            Ids = ModelDb.CardPool<HeathcliffCardPool>().AllCards
                .Select(c => c.Id).ToHashSet();
            GD.Print($"[WildHunt] 预计算狂猎卡 ID 表: {Ids.Count} 张");
        }
        catch (Exception ex)
        {
            GD.PushError($"[WildHunt] 预计算 ID 表失败: {ex.Message}");
            Ids = new HashSet<ModelId>();
        }
    }
}

// ── 补丁 1：_Ready Postfix ──
[HarmonyPatch(typeof(NCardLibrary), "_Ready")]
internal static class PatchCardLibraryInjectHeathcliff_Ready
{
    private static void Postfix(NCardLibrary __instance)
    {
        try
        {
            var grid = ((Node)__instance).GetNode<GridContainer>("Sidebar/MarginContainer/TopVBox/PoolFilters");
            var cardPoolFiltersField = typeof(NCardLibrary).GetField("_cardPoolFilters",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var cardPoolFilters = (Dictionary<CharacterModel, NCardPoolFilter>)
                cardPoolFiltersField!.GetValue(__instance);

            var hc = ModelDb.Character<Heathcliff>();
            if (cardPoolFilters.ContainsKey(hc)) return;

            // 创建新 filter
            var scene = GD.Load<PackedScene>("res://scenes/screens/card_library/library_pool_toggle.tscn");
            var filter = scene.Instantiate<NCardPoolFilter>(PackedScene.GenEditState.Disabled);
            ((Node)filter).Name = "HeathcliffPool";

            var img = ((Node)filter).GetNode<TextureRect>("Image");
            img.Texture = ResourceLoader.Exists("res://images/ui/top_panel/character_icon_heathcliff.png")
                ? GD.Load<Texture2D>("res://images/ui/top_panel/character_icon_heathcliff.png")
                : GD.Load<Texture2D>("res://images/ui/top_panel/character_icon_ironclad.png");

            ((Node)grid).AddChild((Node)(object)filter, false, Node.InternalMode.Disabled);
            cardPoolFilters[hc] = filter;

            // 注入 _poolFilters
            var poolFiltersField = typeof(NCardLibrary).GetField("_poolFilters",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var poolFilters = (Dictionary<NCardPoolFilter, Func<CardModel, bool>>)
                poolFiltersField!.GetValue(__instance);

            // ⚡ 关键：用预计算的 HashSet 查 ID，完全避开 per-card getter
            poolFilters[filter] = c => HeathcliffCardIdSet.Ids.Contains(c.Id);

            // 信号连接
            var anyExisting = cardPoolFilters.Values.First();
            var signalList = ((GodotObject)anyExisting).GetSignalConnectionList("Toggled");
            foreach (var conn in signalList)
            {
                if (conn.TryGetValue("callable", out var v) && v.VariantType == Variant.Type.Callable)
                {
                    var cb = v.As<Callable>();
                    try { ((GodotObject)filter).Connect("Toggled", cb, 0u); }
                    catch (Exception) { }
                }
            }

            filter.Visible = true;
            filter.IsSelected = false;

            // 调 UpdateFilter 刷新
            typeof(NCardLibrary).GetMethod("UpdateFilter",
                BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(__instance, new object[] { false });

            GD.Print("[WildHunt] CardLibrary 注入成功");
        }
        catch (Exception ex)
        {
            GD.PushError($"[WildHunt] CardLibrary 注入失败: {ex.GetType().Name}: {ex.Message}");
        }
    }
}

// ── 补丁 2：OnSubmenuOpened Prefix 兜底 ──
[HarmonyPatch(typeof(NCardLibrary), "OnSubmenuOpened")]
internal static class PatchCardLibraryInjectHeathcliff_Submenu
{
    private static void Prefix(NCardLibrary __instance)
    {
        try
        {
            var f = typeof(NCardLibrary).GetField("_cardPoolFilters",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var dict = (Dictionary<CharacterModel, NCardPoolFilter>)f!.GetValue(__instance);
            var hc = ModelDb.Character<Heathcliff>();

            if (!dict.ContainsKey(hc))
            {
                dict[hc] = dict[ModelDb.Character<Ironclad>()];
                ((CanvasItem)dict[hc]).Visible = true;
                GD.Print("[WildHunt] 兜底注入：复用 Ironclad filter");
            }
            else
            {
                if (!dict[hc].Visible) dict[hc].Visible = true;
            }

            // ── 诊断：检查 _grid._allCards 里狂猎稀有卡的状态 ──
            DumpGridState(__instance);
        }
        catch (Exception ex)
        {
            GD.Print($"[WildHunt] 兜底异常: {ex.Message}");
        }
    }

    private static void DumpGridState(NCardLibrary instance)
    {
        try
        {
            var gridField = typeof(NCardLibrary).GetField("_grid",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (gridField == null) return;
            var grid = gridField.GetValue(instance);
            if (grid == null) return;

            // 读取 grid._allCards 私有字段
            var allCardsField = grid.GetType().GetField("_allCards",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (allCardsField == null) return;
            var allCards = (List<CardModel>)allCardsField.GetValue(grid);
            if (allCards == null) return;

            // 读取 grid._unlockedCards
            var unlockedField = grid.GetType().GetField("_unlockedCards",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var unlocked = unlockedField?.GetValue(grid) as HashSet<CardModel>;

            // 读取 grid._seenCards
            var seenField = grid.GetType().GetField("_seenCards",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var seen = seenField?.GetValue(grid) as HashSet<ModelId>;

            // 统计狂猎卡在各 rarity 上的数量
            int hcCount = 0;
            int hcRareCount = 0;
            int hcRareUnlocked = 0;
            int hcRareSeen = 0;
            foreach (var c in allCards)
            {
                if (HeathcliffCardIdSet.Ids.Contains(c.Id))
                {
                    hcCount++;
                    if (c.Rarity == MegaCrit.Sts2.Core.Entities.Cards.CardRarity.Rare)
                    {
                        hcRareCount++;
                        if (unlocked?.Contains(c) == true) hcRareUnlocked++;
                        if (seen?.Contains(c.Id) == true) hcRareSeen++;
                    }
                }
            }
            GD.Print($"[WildHunt] grid._allCards 狂猎卡: {hcCount} 张, 其中 Rare: {hcRareCount} (unlocked={hcRareUnlocked} seen={hcRareSeen})");

            // 检查 _cards 里狂猎卡的数量（当前显示的）
            var cardsField = grid.GetType().BaseType?.GetField("_cards",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (cardsField != null)
            {
                var cards = (List<CardModel>)cardsField.GetValue(grid);
                if (cards != null)
                {
                    var displayCount = cards.Count(c => HeathcliffCardIdSet.Ids.Contains(c.Id));
                    GD.Print($"[WildHunt] grid._cards 狂猎卡: {displayCount} 张");
                }
            }
        }
        catch (Exception ex)
        {
            GD.Print($"[WildHunt] GridState 诊断异常: {ex.Message}");
        }
    }
}
