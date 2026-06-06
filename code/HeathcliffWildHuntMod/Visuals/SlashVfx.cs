using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace HeathcliffWildHuntMod.Visuals;

/// <summary>
///     攻击命中剑气特效（第一版：纯贴图淡入淡出）。
///     <para>
///     用原版解包的剑气图层贴图（<c>skill1_2_e2</c> → <c>images/effect/attack/slash_s1.png</c>）做命中反馈。
///     原版剑气是「粒子 + shader 让单张形状图动起来」，本版先用最简的「单张贴图 + 淡入/保持/淡出 + 轻微放大」
///     把整条管线跑通看效果；后续可换 shader 溶解/流光层（复刻 CoffinAura 那套手法）提升质感。
///     </para>
///     <para>
///     挂载方式：卡牌在 <c>WithHitVfxNode(创建回调)</c> 里 new 本节点，vanilla 把它 AddChild 进
///     <c>CombatVfxContainer</c>（全局战斗特效容器，非敌人本地容器），时点 = 攻击动画命中延迟之后、伤害结算前一刻。
///     因为容器是全局的，本节点要用<b>全局坐标</b>定位到目标的 <c>VfxSpawnPosition</c>（敌人身上的命中点 Marker）。
///     </para>
///     <para>
///     约束（同 <see cref="CoffinAura" />）：本 mod DLL 未接 Godot.SourceGenerators，自定义 Node 的
///     <c>_Process</c> 不会被引擎回调，故改订阅 <see cref="SceneTree.ProcessFrame" /> 自己用 delta 推进动画。
///     </para>
/// </summary>
internal sealed partial class SlashVfx : Sprite2D
{
    // 剑气贴图资源路径（S1 斩击，353×699）
    private const string SlashTexPath = "res://images/effect/attack/slash_s1.png";

    // ── 时序（秒）：淡入 → 保持 → 淡出，总时长 = 三者之和 ──
    private const double FadeInSeconds = 0.06;  // 快速亮出，命中瞬间要"啪"地出现
    private const double HoldSeconds = 0.08;     // 短暂保持峰值
    private const double FadeOutSeconds = 0.22;  // 稍长的拖尾消散

    // 峰值放大：剑气出现时从略小放大到此倍数，制造"挥出"的扩张感
    private const float StartScale = 0.85f;
    private const float PeakScale = 1.15f;

    // 提亮：剑气偏冷白紫，用 Modulate 稍微加亮（先不上 blend_add，纯贴图看效果）
    private static readonly Color SlashTint = new(1.25f, 1.15f, 1.45f, 1.0f);

    private double _elapsed;
    private bool _subscribed;
    private readonly double _totalSeconds = FadeInSeconds + HoldSeconds + FadeOutSeconds;

    // 命中点全局坐标：CreateFor 时记下，入树后首帧再校准一次（防 AddChild 当帧坐标未就绪）
    private Vector2 _spawnPos;
    private bool _hasSpawnPos;
    private bool _posCalibrated;

    /// <summary>
    ///     在目标 <paramref name="target" /> 身上创建一个 S1 剑气特效节点。
    ///     供卡牌 <c>WithHitVfxNode(c =&gt; SlashVfx.CreateFor(c))</c> 调用。
    /// </summary>
    public static SlashVfx? CreateFor(Creature target)
    {
        if (target is null) return null;

        var tex = GD.Load<Texture2D>(SlashTexPath);
        if (tex is null)
        {
            GD.Print($"[WildHunt][SlashVfx] 贴图加载失败：{SlashTexPath}（检查是否已 Godot 导入）。");
            return null;
        }

        var vfx = new SlashVfx
        {
            Name = "WildHuntSlashVfx",
            Texture = tex,
            Centered = true,
            ZIndex = 5, // 画在敌人模型之上，确保命中剑气可见
            SelfModulate = new Color(SlashTint.R, SlashTint.G, SlashTint.B, 0f), // 初始全透明，淡入
            Scale = new Vector2(StartScale, StartScale),
        };

        // 定位到目标命中点：容器是全局 CombatVfxContainer，必须用全局坐标。
        var node = target.GetCreatureNode();
        if (node is not null && GodotObject.IsInstanceValid(node))
        {
            // 记下命中点，AddChild 入树后首帧再校准一次（AddChild 当帧 GlobalPosition 可能还没就绪），
            // 这里也先设一次，避免第 0 帧出现在原点 (0,0) 闪一下。
            vfx._spawnPos = node.VfxSpawnPosition;
            vfx._hasSpawnPos = true;
            vfx.GlobalPosition = node.VfxSpawnPosition;
        }

        // 关键：本 csproj 没接 Godot.SourceGenerators，C# override 的 _Ready 不会被 Godot vtable 回调
        //（实测 CueFrameSequencePlayer 同样踩过这个坑）。所以这里在纯 C# 路径里立刻订阅 ProcessFrame，
        // 不依赖 _Ready。此刻节点还没被 vanilla AddChild 入树，GetTree() 会是 null，
        // 故走 Engine.GetMainLoop() 兜底——ProcessFrame 是 SceneTree 的全局信号，
        // 不论本节点是否在树中都会每空闲帧触发一次。
        vfx.EnsureSubscribed();
        return vfx;
    }

    private void EnsureSubscribed()
    {
        if (_subscribed) return;
        // 优先用入树后的 GetTree()；拿不到（尚未 AddChild）则用 Engine.GetMainLoop() 强取 SceneTree。
        var tree = GetTree() ?? Engine.GetMainLoop() as SceneTree;
        if (tree is null)
        {
            GD.Print("[WildHunt][SlashVfx] 找不到 SceneTree，无法订阅 ProcessFrame，动画不会推进。");
            return;
        }
        tree.ProcessFrame += OnProcessFrame;
        _subscribed = true;
    }

    // 上一次 ProcessFrame 的时间戳（毫秒）；0 = 尚未起算
    private ulong _lastTickMsec;

    /// <summary>SceneTree.ProcessFrame 回调：自己用 Time.GetTicksMsec 算 delta，推进淡入/保持/淡出。</summary>
    private void OnProcessFrame()
    {
        // 节点已被销毁则退订（防御）
        if (!GodotObject.IsInstanceValid(this))
        {
            Unsubscribe();
            return;
        }

        // 首次订阅可能发生在 AddChild 之前，入树后第一帧再把全局坐标校准一次，
        // 避免 AddChild 当帧 GlobalPosition 还没就绪导致剑气定位偏到原点。
        if (!_posCalibrated && _hasSpawnPos && IsInsideTree())
        {
            GlobalPosition = _spawnPos;
            _posCalibrated = true;
        }

        var now = Time.GetTicksMsec();
        double delta = _lastTickMsec == 0 ? 0 : (now - _lastTickMsec) / 1000.0;
        _lastTickMsec = now;
        _elapsed += delta;

        // ── 计算当前 alpha 与 scale ──
        float alpha;
        float scale;
        if (_elapsed < FadeInSeconds)
        {
            // 淡入：alpha 0→1，scale StartScale→PeakScale
            var t = (float)(_elapsed / FadeInSeconds);
            alpha = t;
            scale = Mathf.Lerp(StartScale, PeakScale, t);
        }
        else if (_elapsed < FadeInSeconds + HoldSeconds)
        {
            // 保持峰值
            alpha = 1f;
            scale = PeakScale;
        }
        else if (_elapsed < _totalSeconds)
        {
            // 淡出：alpha 1→0，scale 继续略微扩张（消散感）
            var t = (float)((_elapsed - FadeInSeconds - HoldSeconds) / FadeOutSeconds);
            alpha = 1f - t;
            scale = Mathf.Lerp(PeakScale, PeakScale + 0.1f, t);
        }
        else
        {
            // 播完：销毁自身
            Unsubscribe();
            QueueFree();
            return;
        }

        SelfModulate = new Color(SlashTint.R, SlashTint.G, SlashTint.B, alpha);
        Scale = new Vector2(scale, scale);
    }

    private void Unsubscribe()
    {
        if (!_subscribed) return;
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree is not null) tree.ProcessFrame -= OnProcessFrame;
        _subscribed = false;
    }
}
