using Godot;

namespace HeathcliffWildHuntMod.Visuals.Definition;

/// <summary>
/// 帧动画里的单帧描述：贴图路径 + 该帧持续秒数 + 可选位移偏移。
/// 设计为 readonly record struct，零分配、值相等，方便放进数组和字典 key。
/// </summary>
/// <param name="TexturePath">Godot 资源路径（res:// 开头），由 ResourceLoader 加载为 Texture2D。</param>
/// <param name="DurationSeconds">该帧停留时长（秒），&lt;=0 视为立即切换到下一帧。</param>
/// <param name="Offset">
/// 该帧相对 Sprite2D <b>初始位置</b>（即序列开播那一刻的 Sprite2D.Position）的偏移量。
/// <c>null</c> 表示不偏移、保持初始位置。
/// 用途：当多帧的 alpha bbox 形状差异很大（比如变身演出某帧角色横跨整个画布），
///       居中粘贴后角色主体会左右漂移，可以用 Offset 把每帧整体推回去对齐。
/// </param>
public readonly record struct VisualFrame(
    string TexturePath,
    float DurationSeconds,
    Vector2? Offset = null);
