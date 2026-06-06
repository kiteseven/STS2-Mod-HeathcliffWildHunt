using HarmonyLib;
using HeathcliffWildHuntMod.Powers;
using HeathcliffWildHuntMod.Relics;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;

namespace HeathcliffWildHuntMod;

// ────────────────────────────────────────
//  Power 图标 patch
// ────────────────────────────────────────

/// <summary>
/// 把本 mod 的 Power/Relic 图标路径覆盖到 <c>res://images/*.png</c>。
/// <para>
/// 背景：vanilla PowerModel/RelicModel 都走 atlas sprite 命名约定，mod 自定义的类型
/// 不在 atlas 里 → ResourceLoader 找 atlas 失败 → missing_power/missing_relic 图 + WARNING。</para>
/// <code>
///   public string PackedIconPath => "atlases/power_atlas.sprites/" + Id.Entry.ToLowerInvariant() + ".tres";
///   public string IconPath       => PackedIconPath;          // 转发；patch 这个等于白 patch
///   public Texture2D Icon        => ResourceLoader.Load(PackedIconPath, ...);  // UI 真正走的入口
/// </code>
/// 也就是说 UI 取 power 图标时调的是 <see cref="PowerModel.Icon"/>，内部用 <c>PackedIconPath</c>，
/// 拼出 <c>res://images/atlases/power_atlas.sprites/coffin_power.tres</c>——atlas 里没有就 missing
/// （日志里 [WARN] AtlasResourceLoader: Missing sprite 'coffin_power' in power_atlas）。
/// </para>
/// <para>
/// 之前 patch 的是 <c>get_IconPath</c>（只是一行转发）所以无效。这里改成同时 patch
/// <c>get_PackedIconPath</c>（UI 主路径）和 <c>get_IconPath</c>（兜底 / 兼容老调用），
/// 直接返回 <c>.png</c> —— <see cref="Godot.ResourceLoader"/> 既能加载 .tres 也能加载 .png。
/// </para>
/// <para>
/// 后续若新增 Power（如隐藏的 <see cref="HeathcliffStatePower"/> 不需要图标），在 <see cref="ResolveIconFor"/>
/// 里加一条 case 即可，两个 Postfix 共用同一个映射方法。
/// </para>
/// </summary>
internal static class PatchHeathcliffPowerIconPath
{
    // 现有图标资源都在 res://images/powers/ 下；集中常量化便于替换
    private const string CoffinIcon   = "res://images/powers/Coffin.png";
    private const string DullahanIcon = "res://images/powers/Dullahan.png";
    // 英文文件名：对照 Limbus Company 官方 EN 翻译（避免 Godot 导出中文路径问题）
    private const string SinkingIcon  = "res://images/powers/sinking.png";
    private const string SanityIcon   = "res://images/powers/Public_Panic.png";
    private const string ErosionIcon  = "res://images/powers/sinking_deluge.png";
    private const string FadedPromiseIcon         = "res://images/powers/faded_promise.png";
    private const string LoveAndHateIcon          = "res://images/powers/love_and_hate.png";
    private const string DecapitateHeathcliffIcon = "res://images/powers/decapitate_heathcliff.png";
    private const string NightmareAvengerReturnsIcon    = "res://images/powers/Public_Panic.png";
    // 狂猎随从被动 buff 图标（挂在随从身上，效果作用于狂猎本体）
    private const string IshmaelBuffIcon = "res://images/powers/IshmaelBuffPower.png";
    private const string GregorBuffIcon  = "res://images/powers/GregorBuffPower.png";
    private const string RyoshuBuffIcon  = "res://images/powers/RyoshuBuffPower.png";
    // 浮士德/奥提斯用 Limbus 庄园回响(manor echo) EGO 命名的素材
    private const string FaustBuffIcon   = "res://images/powers/manor_echo_faust.png";
    private const string OutisBuffIcon   = "res://images/powers/manor_echo_outis.png";
    // 能力卡(Power 类型)授予的持久 power 图标
    private const string GrudgeFireIcon   = "res://images/powers/GrudgeFirePower.png";
    private const string MourningKingIcon = "res://images/powers/MourningKingPower.png";
    private const string WildHuntKingIcon = "res://images/powers/WildHuntKingPower.png";
    // 觉悟(Resolve)先古卡授予的「靠近的勇气」+ 其每回合发放的「守护」
    private const string CourageNearbyIcon = "res://images/powers/CourageNearbyPower.png";
    private const string GuardIcon         = "res://images/powers/GuardPower.png";
    // 拘束 EGO 授予的「缚王御前」(单体残局追打) + 其「理智枷锁」倒计时 debuff
    private const string BoundKingIcon     = "res://images/powers/BoundKingPower.png";
    // 理智枷锁暂无专属图，先复用沉沦激流(sinking_deluge)作占位（同为理智代价类 debuff）
    private const string BindsCountdownIcon = "res://images/powers/sinking_deluge.png";

    /// <summary>按 Power 实例类型选图标；返回 null 表示不替换，沿用 vanilla 拼出来的 atlas 路径。</summary>
    private static string? ResolveIconFor(PowerModel power) => power switch
    {
        CoffinPower              => CoffinIcon,
        DullahanPower            => DullahanIcon,
        SinkingPower             => SinkingIcon,
        SanityPower              => SanityIcon,
        FadedPromisePower        => FadedPromiseIcon,
        LoveAndHatePower         => LoveAndHateIcon,
        DecapitateHeathcliffPower => DecapitateHeathcliffIcon,
        NightmareAvengerReturnsPower   => NightmareAvengerReturnsIcon,
        ErosionPower   => ErosionIcon,
        // 狂猎随从被动 buff
        IshmaelBuffPower => IshmaelBuffIcon,
        GregorBuffPower  => GregorBuffIcon,
        RyoshuBuffPower  => RyoshuBuffIcon,
        FaustBuffPower   => FaustBuffIcon,
        OutisBuffPower   => OutisBuffIcon,
        // 能力卡授予的持久 power
        GrudgeFirePower  => GrudgeFireIcon,
        MourningKingPower => MourningKingIcon,
        WildHuntKingPower => WildHuntKingIcon,
        // 觉悟先古卡：靠近的勇气 + 守护
        CourageNearbyPower => CourageNearbyIcon,
        GuardPower         => GuardIcon,
        // 拘束 EGO：缚王御前 + 理智枷锁
        BoundKingPower      => BoundKingIcon,
        BindsCountdownPower => BindsCountdownIcon,
        // HeathcliffStatePower 是隐藏 Power（IsVisibleInternal=false），UI 不会请求它的图标
        _              => null,
    };

    /// <summary>UI 真正取图标的路径（PowerModel.Icon → ResourceLoader.Load(PackedIconPath, ...)）。</summary>
    [HarmonyPatch(typeof(PowerModel), "get_PackedIconPath")]
    internal static class PackedIconPathPatch
    {
        private static void Postfix(PowerModel __instance, ref string __result)
        {
            // 仅当 instance 命中本 mod 自定义 Power 时覆盖；其他 Power 保持原 atlas 路径
            if (ResolveIconFor(__instance) is { } overridePath)
            {
                __result = overridePath;
            }
        }
    }

    /// <summary>兜底：少数老代码路径直接读 IconPath；保持与 PackedIconPath 一致。</summary>
    [HarmonyPatch(typeof(PowerModel), "get_IconPath")]
    internal static class IconPathPatch
    {
        private static void Postfix(PowerModel __instance, ref string __result)
        {
            if (ResolveIconFor(__instance) is { } overridePath)
            {
                __result = overridePath;
            }
        }
    }

    /// <summary>
    /// 大图标(power tooltip 弹出来的大图)：vanilla <see cref="PowerModel.ResolvedBigIconPath"/>
    /// 找不到 res://images/powers/<id>.png 时回退到 missing_power.png。
    /// 我们自定义的 Power 没有按 atlas 命名规则提供大图，所以 vanilla 走 missing 分支。
    /// 这里 Postfix 直接把 __result 替换成与小图同一张 png（vanilla 的 BigIcon 走 PreloadManager.Cache 加载，
    /// 同样支持 .png 路径），让 tooltip 弹窗也显示正确图标而不是默认问号。
    /// </summary>
    [HarmonyPatch(typeof(PowerModel), "get_ResolvedBigIconPath")]
    internal static class ResolvedBigIconPathPatch
    {
        private static void Postfix(PowerModel __instance, ref string __result)
        {
            if (ResolveIconFor(__instance) is { } overridePath)
            {
                __result = overridePath;
            }
        }
    }
}

// Relic 图标不再需要 Harmony patch——已在 CatherineCoffinRelic / CleanAllCathyRelic 类里
// 直接 override PackedIconPath / PackedIconOutlinePath / BigIconPath。