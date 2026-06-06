using System;
using System.Collections.Generic;
using Godot;

namespace HeathcliffWildHuntMod.Audio;

/// <summary>
///     狂猎 mod 的音频服务（自实现，不依赖任何前置库）。
///     方案：FMOD 裸文件加载（load_file_as_sound / create_sound_instance / play），<b>不走 bank/事件/GUID</b>。
///
///     线程安全：FMOD GDExtension 非线程安全，必须主线程调用；出牌等逻辑在异步 worker 线程，
///     故所有 create_sound_instance + play 都用 CallDeferred / SceneTreeTimer 切主线程。
///
///     系列连播：攻击池是 string[][]（外层随机选一个系列，内层顺序连播）。连播靠读 WAV 头算时长 +
///     SceneTreeTimer 按累计延迟在主线程依次播放，实现无缝衔接。
/// </summary>
internal static class WildHuntAudioService
{
    /// <summary>统一播放音量（0~1）。用户要求 0.7。</summary>
    private const float Volume = 0.7f;

    private static readonly object Gate = new();
    private static bool _preloadAttempted;
    private static bool _warnedNoServer;
    private static readonly Random Rng = new();

    /// <summary>已成功 load_file_as_sound 的文件路径集合（避免重复加载）。</summary>
    private static readonly HashSet<string> LoadedSounds = new(StringComparer.Ordinal);

    /// <summary>WAV 路径 → 时长秒数缓存（连播排程用）。</summary>
    private static readonly Dictionary<string, float> DurationCache = new(StringComparer.Ordinal);

    /// <summary>
    ///     幂等预加载：把所有音效 wav 用 load_file_as_sound 载入 FMOD。FmodServer 未就绪时延后重试。
    /// </summary>
    public static void EnsureLoaded()
    {
        lock (Gate)
        {
            if (_preloadAttempted)
                return;

            var server = FmodInterop.TryGetServer();
            if (server is null)
            {
                if (!_warnedNoServer)
                {
                    GD.Print("[WildHunt][Audio] FmodServer 尚未就绪，音频预加载延后到首次播放前重试。");
                    _warnedNoServer = true;
                }

                return;
            }

            var all = new List<string>();
            foreach (var pool in WildHuntSfx.AllAttackPools) // 攻击池：池→系列→片段
                foreach (var series in pool)
                    all.AddRange(series);
            foreach (var grp in WildHuntSfx.AllSingleGroups) // 单发组
                all.AddRange(grp);

            int ok = 0, miss = 0;
            foreach (var path in all)
            {
                if (LoadFileAsSound(server, path)) ok++;
                else miss++;
            }

            if (ok > 0)
            {
                _preloadAttempted = true;
                GD.Print($"[WildHunt][Audio] 音效预加载完成：成功 {ok} 个，失败 {miss} 个。");
            }
            else
            {
                GD.PushWarning($"[WildHunt][Audio] 音效预加载未成功（成功 0/失败 {miss}）。下次播放前重试。");
            }
        }
    }

    /// <summary>load_file_as_sound 加载单文件；不存在或失败返回 false；已加载直接成功。</summary>
    private static bool LoadFileAsSound(GodotObject server, string resPath)
    {
        if (LoadedSounds.Contains(resPath))
            return true;

        if (!Godot.FileAccess.FileExists(resPath))
        {
            GD.PushWarning($"[WildHunt][Audio] 音频文件不存在（需 Keep 模式裸文件并进 PCK）: {resPath}");
            return false;
        }

        try
        {
            server.Call(FmodInterop.LoadFileAsSound, resPath);
            LoadedSounds.Add(resPath);
            return true;
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[WildHunt][Audio] load_file_as_sound 失败 {resPath}: {ex.Message}");
            return false;
        }
    }

    // PLACEHOLDER_PLAYBACK

    /// <summary>
    ///     延迟 delaySeconds 秒后从单发组随机播一个（用 SceneTreeTimer）。
    ///     用于"杜拉罕入场动画播完再放骑乘音"。拿不到 SceneTree 则立即播兜底。
    /// </summary>
    public static void PlayOneShotDelayed(string[] candidates, float delaySeconds)
    {
        if (candidates is null || candidates.Length == 0)
            return;

        EnsureLoaded();

        if (delaySeconds <= 0f)
        {
            PlayOneShot(candidates);
            return;
        }

        var tree = TryGetTree();
        if (tree is null)
        {
            PlayOneShot(candidates);
            return;
        }

        var timer = tree.CreateTimer(delaySeconds, processAlways: false);
        timer.Timeout += () => PlayOneShot(candidates);
    }

    /// <summary>
    ///     从单发组随机挑一个文件播放（多数组只有一个元素 = 固定播该文件）。用于 cast/dead/select/杜拉罕入场。
    /// </summary>
    public static void PlayOneShot(string[] candidates)
    {
        if (candidates is null || candidates.Length == 0)
            return;

        EnsureLoaded();
        var path = candidates.Length == 1 ? candidates[0] : candidates[Rng.Next(candidates.Length)];
        PlayFileDeferred(path);
    }

    /// <summary>节流键 → 该键音效"播放结束"的时刻（毫秒）。在此之前的重复请求被吞掉。</summary>
    private static readonly Dictionary<string, ulong> ThrottleUntilMs = new(StringComparer.Ordinal);

    /// <summary>
    ///     "播放中不重叠"节流：同一 throttleKey 的音效正在播（上次起播 + 时长还没到）时，重复请求被跳过。
    ///     用于受击——敌人一轮连续多段攻击只在上一声播完后才会再起新的。
    /// </summary>
    public static void PlayOneShotNoOverlap(string[] candidates, string throttleKey)
    {
        if (candidates is null || candidates.Length == 0)
            return;

        EnsureLoaded();
        var path = candidates.Length == 1 ? candidates[0] : candidates[Rng.Next(candidates.Length)];

        var now = Time.GetTicksMsec();
        lock (Gate)
        {
            if (ThrottleUntilMs.TryGetValue(throttleKey, out var until) && now < until)
                return; // 上一声还没播完，跳过

            // 预约本声的结束时刻 = 现在 + 时长（留 50ms 余量）
            var holdMs = (ulong)(GetWavDuration(path) * 1000f) + 50;
            ThrottleUntilMs[throttleKey] = now + holdMs;
        }

        PlayFileDeferred(path);
    }

    /// <summary>
    ///     从攻击池随机挑一个"系列"，把该系列的片段 <b>顺序连播</b>（按各片段时长用定时器排程）。
    ///     pitch &gt; 1 加速播放（连播延迟同步按 pitch 缩短，保持衔接）。
    /// </summary>
    public static void PlayAttackPool(string[][] pool, float pitch = 1.0f)
    {
        if (pool is null || pool.Length == 0)
            return;

        EnsureLoaded();
        var series = pool.Length == 1 ? pool[0] : pool[Rng.Next(pool.Length)];
        if (series is null || series.Length == 0)
            return;

        if (series.Length == 1)
        {
            PlayFileDeferred(series[0], pitch); // 单片段系列：直接播
            return;
        }

        PlaySeriesSequential(series, pitch); // 多片段：顺序连播
    }

    /// <summary>
    ///     顺序连播一个系列：第 0 段立即播，后续段按前面累计时长用 SceneTreeTimer 延迟触发。
    ///     pitch &gt; 1 时实际播放更快，故延迟也除以 pitch，保持无缝衔接。
    /// </summary>
    private static void PlaySeriesSequential(string[] series, float pitch)
    {
        var tree = TryGetTree();
        // 拿不到 SceneTree 就退化为一次性播第一段，避免卡死
        if (tree is null)
        {
            PlayFileDeferred(series[0], pitch);
            return;
        }

        float cumulative = 0f;
        for (var i = 0; i < series.Length; i++)
        {
            var path = series[i];
            if (i == 0)
            {
                PlayFileDeferred(path, pitch); // 首段立即
            }
            else
            {
                var delay = cumulative;
                // 延迟 delay 秒后播该段（SceneTreeTimer 回调在主线程，安全）
                var timer = tree.CreateTimer(delay, processAlways: false);
                timer.Timeout += () => PlayFileDeferred(path, pitch);
            }

            // 累加本段播放耗时（pitch 加速则实际更短），作为下一段的起播延迟
            cumulative += GetWavDuration(path) / pitch;
        }
    }

    /// <summary>把单个文件的播放排到主线程（CallDeferred）。pitch 控制播放速度。</summary>
    private static void PlayFileDeferred(string resPath, float pitch = 1.0f)
    {
        var server = FmodInterop.TryGetServer();
        if (server is null)
            return;

        lock (Gate)
        {
            if (!LoadedSounds.Contains(resPath) && !LoadFileAsSound(server, resPath))
                return;
        }

        Callable.From(() => CreateAndPlayOnMainThread(server, resPath, pitch)).CallDeferred();
    }

    /// <summary>主线程：create_sound_instance → set_volume → set_pitch → play。</summary>
    private static void CreateAndPlayOnMainThread(GodotObject server, string resPath, float pitch)
    {
        try
        {
            var v = server.Call(FmodInterop.CreateSoundInstance, resPath);
            var sound = v.AsGodotObject();
            if (sound is null || !GodotObject.IsInstanceValid(sound))
            {
                GD.PushWarning($"[WildHunt][Audio] create_sound_instance 返回空 {resPath}。");
                return;
            }

            try { if (sound.HasMethod("set_volume")) sound.Call("set_volume", Volume); } catch { }
            try { if (pitch != 1.0f && sound.HasMethod("set_pitch")) sound.Call("set_pitch", pitch); } catch { }
            sound.Call("play");
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[WildHunt][Audio] 播放失败 {resPath}: {ex.Message}");
        }
    }

    /// <summary>取 SceneTree（主循环）；失败返回 null。</summary>
    private static SceneTree? TryGetTree()
    {
        try { return Engine.GetMainLoop() as SceneTree; }
        catch { return null; }
    }

    /// <summary>
    ///     读 WAV 文件头算时长（秒）。结果缓存。解析失败给个保守默认值，避免连播挤在一起。
    /// </summary>
    private static float GetWavDuration(string resPath)
    {
        lock (Gate)
        {
            if (DurationCache.TryGetValue(resPath, out var cached))
                return cached;
        }

        var dur = ParseWavDuration(resPath);
        lock (Gate)
        {
            DurationCache[resPath] = dur;
        }

        return dur;
    }

    /// <summary>
    ///     解析标准 PCM WAV 头：从 "fmt " chunk 读采样率/通道/位深，从 "data" chunk 读数据字节数，算时长。
    ///     解析不出来返回 1.0s 兜底。
    /// </summary>
    private static float ParseWavDuration(string resPath)
    {
        try
        {
            using var f = Godot.FileAccess.Open(resPath, Godot.FileAccess.ModeFlags.Read);
            if (f is null)
                return 1.0f;

            var bytes = f.GetBuffer((long)f.GetLength());
            if (bytes.Length < 44)
                return 1.0f;

            // 校验 RIFF/WAVE
            if (bytes[0] != 'R' || bytes[1] != 'I' || bytes[2] != 'F' || bytes[3] != 'F')
                return 1.0f;

            int byteRate = 0, dataSize = 0;
            var pos = 12; // 跳过 RIFF(4)+size(4)+WAVE(4)
            while (pos + 8 <= bytes.Length)
            {
                var id = System.Text.Encoding.ASCII.GetString(bytes, pos, 4);
                var sz = BitConverter.ToInt32(bytes, pos + 4);
                var body = pos + 8;
                if (id == "fmt " && body + 16 <= bytes.Length)
                {
                    // byteRate 在 fmt body 偏移 8 处（4字节）= 采样率*通道*位深/8
                    byteRate = BitConverter.ToInt32(bytes, body + 8);
                }
                else if (id == "data")
                {
                    dataSize = sz;
                    break; // data 之后不必再读
                }

                if (sz < 0) break;
                pos = body + sz + (sz & 1); // chunk 偶数对齐
            }

            if (byteRate <= 0 || dataSize <= 0)
                return 1.0f;

            return (float)dataSize / byteRate; // 时长 = 数据字节 / 每秒字节
        }
        catch
        {
            return 1.0f;
        }
    }

    /// <summary>
    ///     创建可长期持有的流式音乐实例（BGM）。返回实例对象，调用方负责 play/stop。须主线程调用。
    /// </summary>
    public static GodotObject? CreateMusicInstance(string resPath)
    {
        var server = FmodInterop.TryGetServer();
        if (server is null)
            return null;

        if (!Godot.FileAccess.FileExists(resPath))
        {
            GD.PushWarning($"[WildHunt][Audio] BGM 文件不存在: {resPath}");
            return null;
        }

        try
        {
            if (!LoadedSounds.Contains(resPath))
            {
                server.Call(FmodInterop.LoadFileAsMusic, resPath);
                LoadedSounds.Add(resPath);
            }

            var v = server.Call(FmodInterop.CreateSoundInstance, resPath);
            return v.AsGodotObject();
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[WildHunt][Audio] 创建 BGM 实例失败 {resPath}: {ex.Message}");
            return null;
        }
    }
}
