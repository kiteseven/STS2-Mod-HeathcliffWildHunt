using System;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace HeathcliffWildHuntMod.Visuals;

/// <summary>
///     棺层数「紫色能量乱流」身后特效，<b>shader 复刻原版 Fx_M_PC_Heathcliff_WildHunt_GraveStackAura</b>。
///     <para>
///     原版实现（解包材质 JSON 得出）：一张噪声图 fx_t_noise_24 走「溶解 + UV 扰动 + 沿溶解边缘描
///     HDR 爆亮紫边(rgb 0.5,0,50) + 加色发光 + 横向流动」。本类用 <c>coffin_aura.gdshader</c> 1:1 复刻，
///     挂在一个 ColorRect 画布上（UV 0~1 跑满，shader 内做径向衰减让乱流聚在角色身后中心）。
///     强度由棺层数 1→10 映射到 shader 的 <c>intensity</c> uniform（0→1），层数越高乱流越炸裂、紫边越多。
///     </para>
///     <para>
///     关键修正历史：① 旧版 ZIndex=-1 被背景盖住 → ZIndex=1。② 旧版云雾/电弧不像原版 →
///     改 shader 复刻原版噪声溶解乱流。③ blend_mix 在无 bloom 的 Godot canvas 下太暗 → 改 blend_add。
///     </para>
///     <para>
///     约束：本 mod DLL 未接 Godot.SourceGenerators，自定义 Node 的 _Process 不会被调用，
///     故跟随定位改订阅 <see cref="SceneTree.ProcessFrame" />（shader 自身的 TIME 驱动流动动画，无需每帧喂）。
///     </para>
/// </summary>
internal sealed class CoffinAura
{
    private const int MaxLayers = 10; // 与 CoffinPower.Cap 一致

    private const string ShaderPath = "res://shaders/coffin_aura.gdshader";
    private const string NoiseTexPath = "res://images/effect/coffin/coffin_noise.png";

    // 参考图：窄高竖柱（晶柱），不是方形。宽度收窄、高度拉长。
    private const float CanvasW = 420f;
    private const float CanvasH = 420f;

    private readonly Node2D _root;
    private readonly ColorRect _canvas;
    private readonly ShaderMaterial _mat;
    private readonly ShaderMaterial _matR; // 右块独立材质：与左块同色同流动，但形状裁切阈值不同（左右解耦）
    private readonly GpuParticles2D _sparks;
    private readonly Creature _owner;

    private int _layers;
    private bool _subscribed;

    private CoffinAura(Creature owner, Node2D root, ColorRect canvas, ShaderMaterial mat, ShaderMaterial matR, GpuParticles2D sparks)
    {
        _owner = owner;
        _root = root;
        _canvas = canvas;
        _mat = mat;
        _matR = matR;
        _sparks = sparks;
    }

    public int Layers => _layers;
    public bool IsValid => GodotObject.IsInstanceValid(_root);

    private static readonly System.Collections.Generic.Dictionary<Creature, CoffinAura> Instances = new();

    /// <summary>更新某 creature 的棺乱流特效到指定层数。首次惰性创建；容器未就绪静默跳过。</summary>
    public static void UpdateFor(Creature owner, int layers)
    {
        if (owner is null) return;

        if (Instances.TryGetValue(owner, out var aura))
        {
            if (!aura.IsValid) Instances.Remove(owner);
            else { aura.SetLayers(layers); return; }
        }

        var created = TryCreate(owner);
        if (created is null)
        {
            GD.Print($"[WildHunt][CoffinAura] UpdateFor(layers={layers}) 创建失败：BackVfx 容器/shader 未就绪，等下次重试。");
            return;
        }
        Instances[owner] = created;
        created.SetLayers(layers);
        GD.Print($"[WildHunt][CoffinAura] 已创建并设层数={layers}（shader 乱流版）。");
    }

    public static void ClearFor(Creature owner)
    {
        if (owner is not null && Instances.TryGetValue(owner, out var aura))
        {
            aura.Dispose();
            Instances.Remove(owner);
        }
    }

    public static void ClearAll()
    {
        foreach (var aura in Instances.Values) aura.Dispose();
        Instances.Clear();
    }

    /// <summary>在玩家模型身后创建 shader 乱流画布。容器取自 GetBackVfxContainer()；拿不到返回 null。</summary>
    public static CoffinAura? TryCreate(Creature owner)
    {
        if (owner is null) return null;
        var container = owner.GetBackVfxContainer();
        if (container is null || !GodotObject.IsInstanceValid(container))
            return null;

        var shader = GD.Load<Shader>(ShaderPath);
        var noiseTex = GD.Load<Texture2D>(NoiseTexPath);
        if (shader is null || noiseTex is null)
        {
            GD.Print($"[WildHunt][CoffinAura] 加载失败 shader={shader is not null} noise={noiseTex is not null}（检查 shaders/coffin_aura.gdshader 与 images/effect/coffin/coffin_noise.png 是否已导入）。");
            return null;
        }

        // ZIndex=0：纯靠场景树顺序定层级。CombatSceneContainer 子序为
        // BgContainer → BackCombatVfxContainer → AllyContainer(角色)，同 z 时后者画在前，
        // 故特效天然在「背景之上、角色之下」= 正好身后。
        // 注意 z_as_relative 默认开：CombatSceneContainer 有 ZIndex=-10，
        // 若给 root 设 -1 会累加成有效 z=-11，反被背景(z=-10)盖住 → 特效消失（上一版的坑）。
        // RotationDegrees=0：暂时去掉倾斜，晶柱完全竖直（用户要先看竖直效果再定方向）。
        // 原版倾斜来自节点旋转（材质 Rotate 全为 0），需要倾斜时改这个负角=逆时针=顶部往左倒。
        var root = new Node2D { Name = "WildHuntCoffinAura", ZIndex = 0, RotationDegrees = -10f };

        var mat = new ShaderMaterial { Shader = shader };
        mat.SetShaderParameter("noise_tex", noiseTex);
        mat.SetShaderParameter("intensity", 0f);
        // ── 原版材质参数 1:1（取自解包 Shader#4541 结构 + 材质 dump）──
        mat.SetShaderParameter("color_tile", new Vector2(5.0f, 5.0f));
        mat.SetShaderParameter("dissolve_tile", new Vector2(5.0f, 5.0f));
        mat.SetShaderParameter("noise_tile", new Vector2(2.0f, 2.0f));
        mat.SetShaderParameter("flow_speed", 0.1f);
        mat.SetShaderParameter("color_hardness", 0.2f);           // _Color_Hardness
        mat.SetShaderParameter("dissolve_hardness", 0.8f);        // _Dissolve_Hardness
        mat.SetShaderParameter("color_noise_multi", 0.1f);        // _Color_Noise_Multi
        mat.SetShaderParameter("dissolve_noise_multi", 0.5f);     // _Dissolve_Noise_Multi
        mat.SetShaderParameter("noise_multi", 0.5f);              // _Main_Noise_Multi
        mat.SetShaderParameter("outline_thickness", 0.15f);
        mat.SetShaderParameter("outline_hardness", 0.99f);
        // ── 颜色：以 Limbus 源码材质为准 ──
        // 材质 dump 里唯一真实颜色字段 = _Outline_Color rgb(0.5, 0, 50)，是 HDR 值（b=50 远超1）。
        // 原版靠 bloom 把它炸成亮蓝紫白边；Godot blend_add canvas 无 HDR 帧缓冲、每通道在 1.0 截断，
        // 故 (0.5,0,50) 加色后被钳成 (0.5,0,1.0)=RGB(128,0,255) 蓝紫——这就是忠于源码的等价描边色。
        mat.SetShaderParameter("outline_color", new Color(0.5f, 0.0f, 1.0f)); // 源码 HDR 钳位等价值
        mat.SetShaderParameter("outline_glow", 5.0f);
        // 碎片本体色：原版材质【无对应字段】，本体是噪声贴图灰度本身、靠场景 bloom 染成蓝紫。
        // Godot 无 bloom，故按源码描边色系手动给本体上色——对齐原版【高饱和品红-紫罗兰】：
        // 暗部很深（深紫黑）、核心高饱和亮紫（非泛白），拉开明暗对比去掉灰糊感。
        mat.SetShaderParameter("body_color", new Color(0.16f, 0.02f, 0.42f));  // 暗部：更深的紫黑
        mat.SetShaderParameter("core_color", new Color(0.95f, 0.30f, 1.55f));  // 核心：高饱和亮紫（品红紫，不再泛白）
        // 左块上部按红线裁切：slope 调小→裁切边更竖直（更陡的 / 左臂），lo/hi 定位apex在中上。
        mat.SetShaderParameter("top_cut_lo", 0.44f);
        mat.SetShaderParameter("top_cut_hi", 0.56f);
        mat.SetShaderParameter("top_cut_slope", 0.55f);

        // 画布：以身后中心为原点，居中铺开
        var canvas = new ColorRect
        {
            Name = "Turbulence",
            Material = mat,
            Size = new Vector2(CanvasW, CanvasH),
            Position = new Vector2(-CanvasW / 2f, -CanvasH / 2f),
            Color = Colors.White,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        root.AddChild(canvas);

        // 右块独立材质 matR：颜色/流动/平铺等全部 1:1 复制左块 mat，仅【裁切阈值】不同，
        // 以便左右块形状独立调（共享一个 mat 时 UV 裁切会镜像联动、无法分别塑形——这是上版的限制）。
        var matR = new ShaderMaterial { Shader = shader };
        matR.SetShaderParameter("noise_tex", noiseTex);
        matR.SetShaderParameter("intensity", 0f);
        matR.SetShaderParameter("color_tile", new Vector2(5.0f, 5.0f));
        matR.SetShaderParameter("dissolve_tile", new Vector2(5.0f, 5.0f));
        matR.SetShaderParameter("noise_tile", new Vector2(2.0f, 2.0f));
        matR.SetShaderParameter("flow_speed", 0.1f);
        matR.SetShaderParameter("color_hardness", 0.2f);
        matR.SetShaderParameter("dissolve_hardness", 0.8f);
        matR.SetShaderParameter("color_noise_multi", 0.1f);
        matR.SetShaderParameter("dissolve_noise_multi", 0.5f);
        matR.SetShaderParameter("noise_multi", 0.5f);
        matR.SetShaderParameter("outline_thickness", 0.15f);
        matR.SetShaderParameter("outline_hardness", 0.99f);
        matR.SetShaderParameter("outline_color", new Color(0.5f, 0.0f, 1.0f));
        matR.SetShaderParameter("outline_glow", 5.0f);
        matR.SetShaderParameter("body_color", new Color(0.16f, 0.02f, 0.42f));
        matR.SetShaderParameter("core_color", new Color(0.95f, 0.30f, 1.55f));
        // 右块：镜像后这条裁切线变成红线右臂 \。slope 同样调陡(0.55)；阈值放低保留更多料，
        // 让右臂顶端往中心 apex 延伸、与左臂在中央交汇（配合 RightAnchor 上移内收补全缺口）。
        matR.SetShaderParameter("top_cut_lo", 0.30f);
        matR.SetShaderParameter("top_cut_hi", 0.44f);
        matR.SetShaderParameter("top_cut_slope", 0.55f);
        // 右下延伸：底部少收窄(taper 2.6→1.2)→下半更长更饱满；侧切阈值调小(0.20/0.30→0.06/0.16)
        // → 镜像后保留更多【屏幕右侧】料，右下角往角色右侧外延，填补红框空缺。
        matR.SetShaderParameter("taper_bottom", 0.2f);
        matR.SetShaderParameter("side_cut_lo", 0.06f);
        matR.SetShaderParameter("side_cut_hi", 0.16f);

        // 右侧副晶柱：用独立材质 matR（颜色/流动/强度仍与左块同步，但形状裁切独立）。
        // 教训：上版把 Scale.X=-0.7 直接设在 ColorRect 上 → ColorRect 缩放绕左上角(pivot 0,0)，
        // 负 X 把 420 宽矩形沿左上角竖轴翻转，整块甩到左边老远。
        // 正解：仿左块结构——用 Node2D 锚点(旋转/缩放绕原点、可预测)定位，
        // 内部挂一块居中的 ColorRect，镜像(Scale.X=-1)只翻纹理不动锚点。
        var rightAnchor = new Node2D
        {
            Name = "RightAnchor",
            Position = new Vector2(57f, -10f),    // 右移+下移：让右块本体落进角色右侧区(肩→腰)
            RotationDegrees = 15f,                // 收倾角：右块更竖直，body 顺着右侧往下铺
            Scale = new Vector2(-0.75f, 0.72f),   // X 负=镜像+加宽(0.75)；Y 收回 0.72（之前 1.05 导致整体往上窜变高）
        };
        var canvasR = new ColorRect
        {
            Name = "TurbulenceR",
            Material = matR,
            Size = new Vector2(CanvasW, CanvasH),
            Position = new Vector2(-CanvasW / 2f, -CanvasH / 2f), // 居中于锚点原点
            Color = Colors.White,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            // SelfModulate 逐节点调制（只作用本块、不影响左块）：压暗 + 压绿通道 → 右块更暗、更偏紫，
            // 与左块拉开主次（左块主、右块作陪衬）。
            SelfModulate = new Color(0.72f, 0.55f, 0.85f),
        };
        rightAnchor.AddChild(canvasR);
        root.AddChild(rightAnchor);

        var sparks = BuildSparks();
        root.AddChild(sparks);

        container.AddChild(root);

        var aura = new CoffinAura(owner, root, canvas, mat, matR, sparks);
        aura.Subscribe();
        return aura;
    }

    private static GpuParticles2D BuildSparks()
    {
        var mat = new ParticleProcessMaterial
        {
            Direction = new Vector3(0, -1, 0),
            Spread = 40f,
            Gravity = new Vector3(0, -30f, 0),
            InitialVelocityMin = 35f,
            InitialVelocityMax = 90f,
            ScaleMin = 0.02f,
            ScaleMax = 0.06f,
            Color = new Color(0.55f, 0.32f, 0.85f, 0.5f),
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere,
            EmissionSphereRadius = 55f,
        };
        return new GpuParticles2D
        {
            Name = "Sparks",
            ProcessMaterial = mat,
            Amount = 12,
            Lifetime = 1.0,
            Emitting = false,
            Texture = BuildDotTexture(),
            LocalCoords = false,
        };
    }

    private static GradientTexture2D BuildDotTexture()
    {
        var grad = new Gradient();
        grad.SetOffset(0, 0f); grad.SetColor(0, new Color(1, 1, 1, 1));
        grad.AddPoint(1f, new Color(1, 1, 1, 0));
        return new GradientTexture2D
        {
            Gradient = grad, Width = 24, Height = 24,
            Fill = GradientTexture2D.FillEnum.Radial,
            FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(0.5f, 0f),
        };
    }

    /// <summary>设置棺层数（0~10）→ 映射到 shader intensity（0~1）+ 火花发射量。</summary>
    public void SetLayers(int layers)
    {
        _layers = Mathf.Clamp(layers, 0, MaxLayers);
        if (!IsValid) return;

        var t = _layers / (float)MaxLayers;
        if (_mat is not null)
            _mat.SetShaderParameter("intensity", t);
        if (_matR is not null) // 右块独立材质同步强度，保持左右随棺层数一起强弱
            _matR.SetShaderParameter("intensity", t);

        if (GodotObject.IsInstanceValid(_sparks))
        {
            _sparks.Emitting = _layers > 0;
            _sparks.Amount = Math.Max(1, (int)(4 + 16 * t));
        }
    }

    private void Subscribe()
    {
        if (_subscribed || !IsValid) return;
        var tree = _root.GetTree();
        if (tree is null) return;
        tree.ProcessFrame += OnProcessFrame;
        _subscribed = true;
    }

    /// <summary>每帧：把特效根定位到模型身后（shader 的 TIME 自己驱动流动动画）。</summary>
    private void OnProcessFrame()
    {
        if (!IsValid) { Dispose(); return; }

        var node = _owner.GetCreatureNode();
        if (node is not null && GodotObject.IsInstanceValid(node))
        {
            // 定位：root 居中点 = 头顶往左 40、往下 150（躯干偏左、整体下移），使竖晶柱长在角色左后侧、纵贯全身。
            try { _root.GlobalPosition = node.GetTopOfHitbox() + new Vector2(-40, 150); }
            catch { /* 战斗切换瞬间忽略 */ }
        }
    }

    public void Dispose()
    {
        if (_subscribed)
        {
            var tree = Engine.GetMainLoop() as SceneTree;
            if (tree is not null) tree.ProcessFrame -= OnProcessFrame;
            _subscribed = false;
        }
        if (GodotObject.IsInstanceValid(_root))
            _root.QueueFree();
    }
}
