using Godot;

namespace HeathcliffWildHuntMod.Visuals.Definition;

/// <summary>
/// 程序化视觉节点的可选样式覆盖：未设置的字段在应用时不会改动目标节点。
/// 用于 cue 播放时给 Sprite2D / Node2D / CanvasItem 统一打点（位置、缩放、调色、翻转等）。
/// </summary>
public sealed record VisualNodeStyle
{
    /// <summary>空样式：应用后不会修改目标节点的任何属性。</summary>
    public static VisualNodeStyle Empty { get; } = new();

    /// <summary>绝对本地位置；为空时若调用方提供 base 位置则使用 base，否则保留节点当前位置。</summary>
    public Vector2? Position { get; init; }

    /// <summary>叠加在 <see cref="Position"/> 或 base 位置之上的偏移量。</summary>
    public Vector2? Offset { get; init; }

    /// <summary>本地缩放（适用于 Node2D / Control）。</summary>
    public Vector2? Scale { get; init; }

    /// <summary>本地旋转，单位弧度（与 Godot 原生一致）。</summary>
    public float? RotationRadians { get; init; }

    /// <summary>Node2D 倾斜。</summary>
    public float? Skew { get; init; }

    /// <summary>Control 节点的 PivotOffset。</summary>
    public Vector2? PivotOffset { get; init; }

    /// <summary>CanvasItem 调制色（整体染色）。</summary>
    public Color? Modulate { get; init; }

    /// <summary>CanvasItem 自身调制色（不影响子节点）。</summary>
    public Color? SelfModulate { get; init; }

    /// <summary>CanvasItem ZIndex（绘制顺序）。</summary>
    public int? ZIndex { get; init; }

    /// <summary>CanvasItem 可见性。</summary>
    public bool? Visible { get; init; }

    /// <summary>Sprite2D 居中标记。</summary>
    public bool? Centered { get; init; }

    /// <summary>Sprite2D 水平翻转。</summary>
    public bool? FlipH { get; init; }

    /// <summary>Sprite2D 垂直翻转。</summary>
    public bool? FlipV { get; init; }

    /// <summary>使用角度（°）创建样式，对作者更友好；内部仍存弧度。</summary>
    public static VisualNodeStyle Create(
        Vector2? position = null,
        Vector2? offset = null,
        Vector2? scale = null,
        float? rotationDegrees = null,
        float? skew = null,
        Vector2? pivotOffset = null,
        Color? modulate = null,
        Color? selfModulate = null,
        int? zIndex = null,
        bool? visible = null,
        bool? centered = null,
        bool? flipH = null,
        bool? flipV = null)
    {
        return new()
        {
            Position = position,
            Offset = offset,
            Scale = scale,
            // 角度→弧度仅在外部输入时转一次，运行时一律走弧度
            RotationRadians = rotationDegrees.HasValue ? Mathf.DegToRad(rotationDegrees.Value) : null,
            Skew = skew,
            PivotOffset = pivotOffset,
            Modulate = modulate,
            SelfModulate = selfModulate,
            ZIndex = zIndex,
            Visible = visible,
            Centered = centered,
            FlipH = flipH,
            FlipV = flipV,
        };
    }

    /// <summary>统一缩放快捷方法。</summary>
    public VisualNodeStyle WithScale(float uniformScale) => this with { Scale = new Vector2(uniformScale, uniformScale) };

    /// <summary>设置可见性。</summary>
    public VisualNodeStyle WithVisible(bool visible = true) => this with { Visible = visible };

    /// <summary>设置翻转标记。</summary>
    public VisualNodeStyle WithFlip(bool? horizontal = null, bool? vertical = null)
        => this with { FlipH = horizontal ?? FlipH, FlipV = vertical ?? FlipV };
}

/// <summary>
/// 把 <see cref="VisualNodeStyle"/> 的字段批量应用到目标 Godot 节点上。
/// 区分 CanvasItem / Node2D / Control / Sprite2D 四类目标，分别只写它们各自支持的字段。
/// </summary>
internal static class VisualNodeStyleApplicator
{
    /// <summary>应用 style 到 target。style 为 null 或 target 已失效时直接跳过。</summary>
    /// <param name="positionBase">外部传入的基准位置：当 style.Position 为空时使用，避免节点回到 (0,0)。</param>
    public static void ApplyTo(this VisualNodeStyle? style, Node? target, Vector2? positionBase = null)
    {
        if (style == null || !GodotObject.IsInstanceValid(target)) return;

        // CanvasItem 通用：可见性 / 调制 / 自调制 / ZIndex
        if (target is CanvasItem canvasItem)
        {
            if (style.Visible.HasValue) canvasItem.Visible = style.Visible.Value;
            if (style.Modulate.HasValue) canvasItem.Modulate = style.Modulate.Value;
            if (style.SelfModulate.HasValue) canvasItem.SelfModulate = style.SelfModulate.Value;
            if (style.ZIndex.HasValue) canvasItem.ZIndex = style.ZIndex.Value;
        }

        // Node2D：position/offset/scale/rotation/skew
        if (target is Node2D node2D)
        {
            if (style.Position.HasValue) node2D.Position = style.Position.Value;
            else if (positionBase.HasValue) node2D.Position = positionBase.Value;
            if (style.Offset.HasValue) node2D.Position += style.Offset.Value;
            if (style.Scale.HasValue) node2D.Scale = style.Scale.Value;
            if (style.RotationRadians.HasValue) node2D.Rotation = style.RotationRadians.Value;
            if (style.Skew.HasValue) node2D.Skew = style.Skew.Value;
        }

        // Control：position/offset/scale/rotation/pivot
        if (target is Control control)
        {
            if (style.Position.HasValue) control.Position = style.Position.Value;
            else if (positionBase.HasValue) control.Position = positionBase.Value;
            if (style.Offset.HasValue) control.Position += style.Offset.Value;
            if (style.Scale.HasValue) control.Scale = style.Scale.Value;
            if (style.RotationRadians.HasValue) control.Rotation = style.RotationRadians.Value;
            if (style.PivotOffset.HasValue) control.PivotOffset = style.PivotOffset.Value;
        }

        // Sprite2D：centered + flip
        if (target is Sprite2D sprite)
        {
            if (style.Centered.HasValue) sprite.Centered = style.Centered.Value;
            if (style.FlipH.HasValue) sprite.FlipH = style.FlipH.Value;
            if (style.FlipV.HasValue) sprite.FlipV = style.FlipV.Value;
        }
    }
}
