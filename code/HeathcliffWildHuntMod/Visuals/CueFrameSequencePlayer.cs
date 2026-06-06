using System;
using Godot;
using HeathcliffWildHuntMod.Visuals.Definition;

namespace HeathcliffWildHuntMod.Visuals;

/// <summary>
/// 帧序列推进器：作为子 Node 挂载在角色根节点下，以游戏帧为时基驱动 <see cref="Sprite2D"/>.Texture 切换。
/// <para>
/// 工作流程：
/// 1. <see cref="EnsureUnder"/> 确保父节点下挂着同名实例（避免重复挂载）；
/// 2. <see cref="TryStart"/> 接收一个 <see cref="VisualFrameSequence"/> + 目标 Sprite2D，开始播放；
/// 3. 每个 <c>_Process(delta)</c> 累加 <see cref="_elapsed"/>，超过当前帧时长则推进到下一帧并写贴图；
/// 4. 非循环序列播完最后一帧后清空状态，发出 <see cref="Finished"/> 信号供上层（CueAnimationBackend）听到。
/// </para>
/// <para>注意：贴图通过 <see cref="ResourceLoader.Load{T}(string, string, ResourceLoader.CacheMode)"/> 同步载入；
/// 角色出场前请确保所有帧资源已存在 res:// 路径下，否则该帧会被跳过。</para>
/// </summary>
public sealed partial class CueFrameSequencePlayer : Node
{
    /// <summary>挂在父节点下的子节点固定名，便于 <see cref="StopUnder"/> 反查。</summary>
    public const string NodeName = nameof(CueFrameSequencePlayer);

    /// <summary>非循环序列播完最后一帧时发出。循环序列永远不发。</summary>
    /// <remarks>
    /// 用普通 C# 事件而非 Godot <c>[Signal]</c>：本 mod 没接 Godot 的 source generator，
    /// <c>SignalName.Finished</c> 在编译期不存在。订阅方都在 C# 侧（CueAnimationBackend），改用 .NET event 更合适。
    /// </remarks>
    public event Action? Finished;

    // 当前正在驱动的 Sprite2D（null = 没在播）
    private Sprite2D? _sprite;
    // 当前序列（null = 没在播）
    private VisualFrameSequence? _sequence;
    // 当前帧索引（0-based）
    private int _frameIndex;
    // 在当前帧上已经累计了多少秒
    private double _elapsed;
    // 序列开播时记录的 Sprite2D.Position；带 Offset 的帧会以此为基准做相对位移
    private Vector2 _initialSpritePos;
    // 序列结束/打断后应瞬移回的位置。默认 = _initialSpritePos（同位置,不覆写时不变）。
    // 攻击 dash 场景：_initialSpritePos 是冲到敌人面前的位置（帧偏移基准），
    // _returnPosition 是原地 idle 位置（播完自动回）。二者解耦。
    private Vector2 _returnPosition;
    private bool _hasOverrideReturnPos;
    // 一次性诊断标记：序列开播后 _Process 第一次进来时打印一行，TryStart 里复位
    private bool _processLogged;

    public CueFrameSequencePlayer()
    {
        // 节点名固定，便于按名查找
        Name = NodeName;
        // STS2 战斗在播角色动画时会把场景树临时暂停（等动画播完才推进游戏逻辑）。
        // 默认 Inherit 会跟着父节点一起冻住，所以设成 Always 保证暂停期间也能推进。
        ProcessMode = ProcessModeEnum.Always;
    }

    /// <summary>
    /// 在 <paramref name="parent"/> 下确保存在唯一的 <see cref="CueFrameSequencePlayer"/> 实例。
    /// 已有则直接返回，没有则 new 一个并 AddChild。AddChild 之后立刻订阅 SceneTree.ProcessFrame。
    /// </summary>
    public static CueFrameSequencePlayer EnsureUnder(Node parent)
    {
        ArgumentNullException.ThrowIfNull(parent);
        // 先按固定 NodeName 查找已挂载的实例，避免重复挂载
        if (parent.GetNodeOrNull<CueFrameSequencePlayer>(NodeName) is { } existing)
        {
            return existing;
        }
        var player = new CueFrameSequencePlayer();
        parent.AddChild(player);
        // 在这里订阅 ProcessFrame，而不是 _Ready：本 csproj 没接 Godot.SourceGenerators，
        // C# override 的 _Ready / _Process 都不会被 Godot vtable 调用（实测 _Process tick 永远没打印）。
        // EnsureUnder 是纯 C# 路径，AddChild 之后节点已进 SceneTree，可以稳定拿到 GetTree()。
        var tree = player.GetTree();
        if (tree != null)
        {
            tree.ProcessFrame += player.OnProcessFrame;
            GD.Print($"[WildHunt] CueFrameSequencePlayer subscribed to SceneTree.ProcessFrame on {parent.Name}");
        }
        else
        {
            GD.Print("[WildHunt] CueFrameSequencePlayer: GetTree() == null after AddChild — animation will not advance!");
        }
        return player;
    }

    /// <summary>是否有正在播放的序列。</summary>
    public bool IsPlaying => _sequence is not null;

    /// <summary>
    /// 设序列结束/打断后应瞬移回的位置。仅攻击 dash 需要（_initialSpritePos 是 dash 位置做帧基准，
    /// _returnPosition 是原始 idle 位置做归位目标）。不调时默认回到 _initialSpritePos。
    /// </summary>
    public void OverrideReturnPosition(Vector2 position)
    {
        _returnPosition = position;
        _hasOverrideReturnPos = true;
    }

    /// <summary>若 <paramref name="parent"/> 下挂着实例，则停止其播放（不删除节点，复用即可）。</summary>
    public static void StopUnder(Node parent)
    {
        if (parent is null) return;
        if (parent.GetNodeOrNull<CueFrameSequencePlayer>(NodeName) is { } existing)
        {
            existing.StopAndReset();
        }
    }

    /// <summary>开始播放新序列。会立刻把第 0 帧贴到 sprite 上，重置已用时长。</summary>
    public bool TryStart(Sprite2D sprite, VisualFrameSequence sequence)
    {
        ArgumentNullException.ThrowIfNull(sprite);
        ArgumentNullException.ThrowIfNull(sequence);
        if (sequence.Frames.Count == 0)
        {
            // 防御性检查：空序列不应能被 builder 构造出来，但万一外部直接 new 也得拦下
            return false;
        }
        _sprite = sprite;
        _sequence = sequence;
        _frameIndex = 0;
        _elapsed = 0;
        _processLogged = false;
        _lastTickMsec = 0;
        _tickCounter = 0;
        // 记录序列开播时的 Sprite2D.Position 作为偏移基准；带 Offset 的帧据此相对偏移
        _initialSpritePos = sprite.Position;
        // 新序列开播时清掉上一次的 return position override（新序列默认回到自己的 _initialSpritePos）
        _hasOverrideReturnPos = false;
        // 立刻把首帧贴上去，避免下一帧才看到画面变化
        ApplyCurrentFrameTexture();
        // 兜底订阅：之前可能因为 EnsureUnder 走"已存在分支"或者 _Ready vtable 没被回调，
        // 导致 ProcessFrame 信号根本没接上。这里每次 TryStart 都补查一次，订阅是幂等的。
        EnsureProcessFrameSubscribed();
        GD.Print($"[WildHunt] CueFrameSequencePlayer.TryStart: frames={sequence.Frames.Count} loop={sequence.Loop} subscribed={_processFrameSubscribed}");
        return true;
    }

    /// <summary>把 ProcessFrame 订阅做成幂等：未订阅时订阅，已订阅时跳过。</summary>
    private void EnsureProcessFrameSubscribed()
    {
        if (_processFrameSubscribed) return;
        var tree = GetTree();
        if (tree == null)
        {
            // 不在 SceneTree 中（理论上 AddChild 之后立刻就在）——这种情况下 ProcessFrame 拿不到，
            // 走兜底：用 Engine.GetMainLoop() 强行取 SceneTree 试试
            if (Engine.GetMainLoop() is SceneTree st)
            {
                st.ProcessFrame += OnProcessFrame;
                _processFrameSubscribed = true;
                GD.Print("[WildHunt] CueFrameSequencePlayer subscribed via Engine.GetMainLoop()");
                return;
            }
            GD.Print("[WildHunt] CueFrameSequencePlayer: cannot find SceneTree to subscribe — animation will not advance!");
            return;
        }
        tree.ProcessFrame += OnProcessFrame;
        _processFrameSubscribed = true;
        GD.Print("[WildHunt] CueFrameSequencePlayer subscribed via GetTree().ProcessFrame");
    }

    /// <summary>停止播放并清空内部状态（不发 Finished 信号——主动停止视为"打断"，由上层另行处理）。</summary>
    public void StopAndReset()
    {
        // 打断时也把 sprite 位置还原；有 override return 则回指定点,否则回基准点
        if (_sprite is not null && GodotObject.IsInstanceValid(_sprite))
        {
            _sprite.Position = _hasOverrideReturnPos ? _returnPosition : _initialSpritePos;
        }
        _sprite = null;
        _sequence = null;
        _frameIndex = 0;
        _elapsed = 0;
        _lastTickMsec = 0;
        // 清掉所有 Finished 订阅：新一段序列开播前，旧订阅必须断干净，
        // 否则切 cue 时旧订阅会被新序列的 Finished 误触发，引发"播完 attack 又意外回到上次注入的 idle"之类的状态错乱。
        Finished = null;
    }

    // 上一次 OnProcessFrame 的时间戳（毫秒），用来算 delta；首次为 0 表示尚未起算
    private ulong _lastTickMsec;
    // 是否已订阅 SceneTree.ProcessFrame；TryStart 里检查并补订阅，避免缓存的 player 节点丢订阅
    private bool _processFrameSubscribed;
    // 调试用：当前序列开始后已经累计的 tick 数；用于判断 ProcessFrame 是否真的在持续回调
    private int _tickCounter;

    /// <summary>
    /// SceneTree.ProcessFrame 信号回调：每个空闲帧调用一次，自己用 Time.GetTicksMsec 算 delta。
    /// </summary>
    private void OnProcessFrame()
    {
        // 没在播或目标 sprite 已被销毁则空转
        if (_sequence is null || _sprite is null || !GodotObject.IsInstanceValid(_sprite))
        {
            _lastTickMsec = 0;
            return;
        }

        var now = Time.GetTicksMsec();
        double delta;
        if (_lastTickMsec == 0)
        {
            // 首次 tick 不知 delta，给 0 让循环至少进一次状态机；下一次再算真实 delta
            delta = 0;
        }
        else
        {
            delta = (now - _lastTickMsec) / 1000.0;
        }
        _lastTickMsec = now;
        _tickCounter++;

        // 调试：每 10 tick 打印一次，验证 ProcessFrame 是否在持续触发
        if (_tickCounter == 1 || _tickCounter % 10 == 0)
        {
            GD.Print($"[WildHunt] OnProcessFrame #{_tickCounter}: delta={delta:F4} elapsed={_elapsed:F4} idx={_frameIndex}/{_sequence.Frames.Count}");
        }

        _elapsed += delta;
        var frames = _sequence.Frames;
        // 用 while 是为了应对某帧时长极短 / 单帧 delta 跨越多帧的极端情况
        while (_sequence is not null && _elapsed >= frames[_frameIndex].DurationSeconds)
        {
            _elapsed -= frames[_frameIndex].DurationSeconds;
            _frameIndex++;
            if (_frameIndex >= frames.Count)
            {
                if (_sequence.Loop)
                {
                    // 循环序列：回到第 0 帧继续推进
                    _frameIndex = 0;
                    ApplyCurrentFrameTexture();
                }
                else
                {
                    // 非循环：停在最后一帧画面上，但清状态、发信号
                    _frameIndex = frames.Count - 1;
                    ApplyCurrentFrameTexture();
                    // 还原位置：有 override return 则回到指定点（攻击 dash 场景），否则回初始基准点。
                    if (_sprite is not null && GodotObject.IsInstanceValid(_sprite))
                    {
                        _sprite.Position = _hasOverrideReturnPos ? _returnPosition : _initialSpritePos;
                    }
                    _sequence = null;
                    _sprite = null;
                    _elapsed = 0;
                    _lastTickMsec = 0;
                    // 通知 CueAnimationBackend 等订阅方序列已结束
                    Finished?.Invoke();
                    return;
                }
            }
            else
            {
                ApplyCurrentFrameTexture();
            }
        }
    }

    /// <summary>把 <see cref="_frameIndex"/> 指向的帧的贴图加载到 sprite 上，并应用 per-frame 偏移。</summary>
    private void ApplyCurrentFrameTexture()
    {
        if (_sequence is null || _sprite is null) return;
        var frame = _sequence.Frames[_frameIndex];
        // 用 ResourceLoader 同步载入；ResourceLoader 内部会缓存，重复 path 不会反复 IO
        var tex = ResourceLoader.Load<Texture2D>(frame.TexturePath);
        if (tex is not null)
        {
            _sprite.Texture = tex;
        }
        // Per-frame 位置偏移：以序列开播时的 Sprite2D.Position 为基准做相对位移；
        // 没填 Offset 的帧自动回到基准点（避免上一帧的偏移残留）。
        _sprite.Position = _initialSpritePos + (frame.Offset ?? Vector2.Zero);
        // 注：tex 为 null 时保持原贴图不变，避免黑屏；上线前应让美术补齐资源
    }
}
