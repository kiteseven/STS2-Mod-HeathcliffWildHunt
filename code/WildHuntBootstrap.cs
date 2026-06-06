using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using HeathcliffWildHuntMod.Audio;
using HeathcliffWildHuntMod.EgoPile;

namespace HeathcliffWildHuntMod;

[ModInitializer("Init")]
public static class WildHuntBootstrap
{
    private const string HarmonyId = "kiteseven.heathcliff_wild_hunt";

    /// <summary>
    /// 本 mod 的 EGO 专属堆 PileType 唯一值。高位 <c>0x4000_0000</c> 避开 vanilla 0–6，
    /// 低位 <c>0x0E60</c>("EGO" 标记) 为本 mod 独占——未来辛克莱 mod 拷贝本框架时<b>必须换一个不同低位</b>，
    /// 否则两 mod 共存会撞 PileType。
    /// </summary>
    private const PileType EgoPileTypeValue = (PileType)0x4000_0E60;

    private static bool _initialized;

    public static void Init()
    {
        if (_initialized) return;
        _initialized = true;

        // EGO 框架配置：登记本 mod 出战角色 + EGO 堆唯一 PileType。必须在战斗开始前完成
        // （patch 应用前调用即可），owner 门禁与 EGO 堆解析都依赖它。
        EgoFramework.Configure<Heathcliff>(EgoPileTypeValue);

        var harmony = new Harmony(HarmonyId);
        int applied = 0, failed = 0;

        foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
        {
            if (!type.GetCustomAttributes(typeof(HarmonyPatch), inherit: true).Any())
                continue;
            try
            {
                new PatchClassProcessor(harmony, type).Patch();
                applied++;
            }
            catch (Exception ex)
            {
                failed++;
                Log.Error("[WildHunt] Harmony patch failed for " + type.Name + ": "
                    + (ex.InnerException?.Message ?? ex.Message));
            }
        }

        Log.Info($"[WildHunt] Harmony patches: {applied} applied, {failed} failed.");

        // 尽力在启动早期加载音频 bank + GUID 映射；若此时 FmodServer 单例尚未就绪，
        // 会保持未加载状态，并在首次播放音效前由 NAudioManager patch 兜底重试（EnsureLoaded 幂等）。
        WildHuntAudioService.EnsureLoaded();

        Log.Info("[WildHunt] 已初始化狂猎希斯克利夫模组。");
    }
}
