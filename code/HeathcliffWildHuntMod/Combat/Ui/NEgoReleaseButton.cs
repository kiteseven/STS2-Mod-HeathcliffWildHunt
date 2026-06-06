using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using HeathcliffWildHuntMod.EgoPile;

namespace HeathcliffWildHuntMod.Combat.Ui;

/// <summary>
/// EGO 主动释放入口按钮（纯代码构造，挂载到能量球容器上方）。
/// </summary>
internal sealed partial class NEgoReleaseButton : Button
{
    private readonly Player _player;

    public NEgoReleaseButton(Player player)
    {
        _player = player;

        // 设置节点名（幂等检测用）
        Name = "EgoReleaseButton";

        // 设置锚点和位置（能量球上方居中）
        AnchorLeft = 0.5f;
        AnchorRight = 0.5f;
        AnchorTop = 0f;
        AnchorBottom = 0f;
        OffsetLeft = -32f;   // 按钮宽度 64，居中偏移 -32
        OffsetRight = 32f;
        OffsetTop = -80f;    // 能量球上方 80 像素
        OffsetBottom = -16f; // 按钮高度 64

        // 创建图标（使用占位纹理，后续可替换为专用图标）
        var icon = new TextureRect
        {
            Name = "Icon",
            Texture = GD.Load<Texture2D>("res://ui/combat/energy_icon.png"), // 占位：能量球图标
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            AnchorLeft = 0f,
            AnchorRight = 1f,
            AnchorTop = 0f,
            AnchorBottom = 1f
        };

        AddChild(icon);

        // 设置悬停提示（后续可本地化）
        TooltipText = "Release EGO / 释放 EGO";
    }

    /// <summary>点击时打开 EGO 选择面板。</summary>
    protected override void OnRelease()
    {
        base.OnRelease();
        // fire-and-forget 异步调用（内部 await 完整流程）
        _ = EgoReleaseController.OpenAndReleaseAsync(_player);
    }
}
