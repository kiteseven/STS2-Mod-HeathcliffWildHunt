using System;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Audio;

namespace HeathcliffWildHuntMod.Audio;

/// <summary>
///     狂猎专属战斗 BGM 控制器：在某场战斗"棺层数首次达到最大层(10)"时，
///     停掉原版战斗音乐并循环播放 mod 自带 BGM；战斗结束时停掉 mod BGM 并恢复原版音乐。
///
///     为什么不走原版 NRunMusicController.PlayCustomMusic：它最终通过 GDScript proxy 的
///     create_event_instance(path) 按字符串路径解析音乐，而 mod bank 无 strings.bank，
///     mod 事件路径无法被解析。故这里自持一个 GUID 创建的事件实例来循环播放（见 WildHuntAudioService），
///     原版音乐用 NRunMusicController.StopMusic() 静音、战斗结束再 UpdateMusic() 恢复。
/// </summary>
internal static class WildHuntBgmController
{
    private static readonly object Gate = new();

    /// <summary>当前持有的 mod BGM 事件实例（FMOD FmodEvent）；为 null 表示未在播放。</summary>
    private static GodotObject? _bgmInstance;

    /// <summary>本场战斗是否已触发过（满足"每场战斗首次"，战斗结束时由 ResetForCombat 复位）。</summary>
    private static bool _triggeredThisCombat;

    /// <summary>
    ///     战斗开始时调用：复位"本场已触发"标志，确保每场战斗都能重新触发一次。
    /// </summary>
    public static void ResetForCombat()
    {
        lock (Gate)
        {
            _triggeredThisCombat = false;
        }
    }

    /// <summary>
    ///     棺层数变化时调用：若本场尚未触发且已达最大层，则切换到 mod BGM。幂等——重复调用只触发一次。
    /// </summary>
    public static void OnCoffinReachedMax()
    {
        lock (Gate)
        {
            if (_triggeredThisCombat)
                return;
            _triggeredThisCombat = true;
        }

        StartModBgm();
    }

    /// <summary>
    ///     战斗结束时调用：停掉 mod BGM 并恢复原版战斗音乐 / 环境音 / 进度轨道。
    /// </summary>
    public static void OnCombatEnd()
    {
        StopModBgm();
        lock (Gate)
        {
            _triggeredThisCombat = false;
        }
    }

    /// <summary>停原版音乐 → 创建并播放 mod BGM 流式实例。全部在主线程执行（FMOD 非线程安全）。</summary>
    private static void StartModBgm()
    {
        // 切主线程：停原版音乐 + 创建并播放 mod BGM
        Callable.From(() =>
        {
            try
            {
                NRunMusicController.Instance?.StopMusic(); // 停原版战斗音乐
            }
            catch (Exception ex)
            {
                GD.PushWarning($"[WildHunt][Audio] 停原版音乐失败: {ex.Message}");
            }

            // 创建 mod BGM 流式实例，持有并 play（循环由文件/实例属性决定）
            var instance = WildHuntAudioService.CreateMusicInstance(WildHuntSfx.Bgm);
            if (instance is null || !GodotObject.IsInstanceValid(instance))
            {
                GD.PushWarning("[WildHunt][Audio] 无法创建 BGM 实例。");
                return;
            }

            try
            {
                if (instance.HasMethod("set_volume")) instance.Call("set_volume", 1.0f);
                instance.Call("play"); // 裸文件实例用 play（非 start）
                lock (Gate)
                {
                    _bgmInstance = instance;
                }
                GD.Print("[WildHunt][Audio] 棺层达最大，已切换至狂猎专属 BGM。");
            }
            catch (Exception ex)
            {
                GD.PushWarning($"[WildHunt][Audio] 启动 BGM 失败: {ex.Message}");
            }
        }).CallDeferred();
    }

    /// <summary>停止 mod BGM 实例，恢复原版战斗音乐 / 环境音 / 进度轨道。全部在主线程执行。</summary>
    private static void StopModBgm()
    {
        GodotObject? instance;
        lock (Gate)
        {
            instance = _bgmInstance;
            _bgmInstance = null;
        }

        // 没在播放 mod BGM 就直接返回：避免在玩家阵亡(已切到 game_over 音乐)时
        // 误调 UpdateMusic 把战斗音乐盖回去，破坏 game over 音乐。
        if (instance is null)
            return;

        // 切主线程：停 BGM + 恢复原版音乐
        Callable.From(() =>
        {
            if (GodotObject.IsInstanceValid(instance))
            {
                try
                {
                    instance.Call("stop");
                }
                catch (Exception ex)
                {
                    GD.PushWarning($"[WildHunt][Audio] 停止 BGM 失败: {ex.Message}");
                }
            }

            // 恢复原版：重新加载 act 音乐 bank + 轨道 + 环境音 + 当前房间进度
            try
            {
                var music = NRunMusicController.Instance;
                if (music is not null)
                {
                    music.UpdateMusic();   // 重载 act bank 与战斗音乐
                    music.UpdateAmbience(); // 恢复环境音
                    music.UpdateTrack();    // 按当前房间设置进度参数
                }
            }
            catch (Exception ex)
            {
                GD.PushWarning($"[WildHunt][Audio] 恢复原版音乐失败: {ex.Message}");
            }
        }).CallDeferred();
    }
}
