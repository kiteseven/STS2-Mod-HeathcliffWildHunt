using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;

namespace HeathcliffWildHuntMod;

/// <summary>
/// 把 <see cref="Heathcliff"/> 在选人按钮 <see cref="NCharacterSelectButton"/> 上强制标记为已解锁。
/// <para>
/// 背景：原版 <c>NCharacterSelectButton.Init</c> 用 <c>unlockState.Characters.Contains(_character)</c>
/// 判断角色是否解锁。即便 <c>UnlockState.Characters</c> 含 mod 角色，部分玩家存档下仍会被识别为锁定 ——
/// 表现就是按钮看着像"可点"，但 <see cref="NCharacterSelectScreen.SelectCharacter"/> 走 IsLocked=true 分支，
/// 不会调用 <c>_lobby.SetLocalCharacter</c>，于是 LocalPlayer.character 始终停留在默认 Ironclad，
/// embark 进游戏后人物还是铁甲战士。
/// </para>
/// <para>
/// 修法：Init Postfix —— 当按钮承载的 character 是 Heathcliff 时，反射把 <c>_isLocked</c> 置 false、
/// <c>_icon.Texture</c> 换回正常贴图、<c>_lock.Visible</c> 关闭。等价于"全局给本 mod 角色发解锁卡"。
/// </para>
/// </summary>
[HarmonyPatch(typeof(NCharacterSelectButton), "Init")]
internal static class PatchNCharacterSelectButtonInit
{
    // 反射缓存：只查一次 FieldInfo，每次按钮 Init 都复用
    private static readonly FieldInfo? IsLockedField = typeof(NCharacterSelectButton)
        .GetField("_isLocked", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? IconField = typeof(NCharacterSelectButton)
        .GetField("_icon", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? LockField = typeof(NCharacterSelectButton)
        .GetField("_lock", BindingFlags.Instance | BindingFlags.NonPublic);

    private static void Postfix(NCharacterSelectButton __instance, CharacterModel character)
    {
        // 只关心本 mod 角色，其它角色（包括 RandomCharacter / 原版 5 人）保持原版行为
        if (character is not Heathcliff) return;

        if (IsLockedField == null)
        {
            // 反射没找到字段说明原版改了实现 —— 保留警告便于以后定位
            Log.Warn("[WildHunt] 找不到 NCharacterSelectButton._isLocked 字段，无法强制解锁本 mod 角色");
            return;
        }

        // 1) 强制把按钮标记为已解锁，覆盖 unlockState.Characters.Contains 的判断结果
        IsLockedField.SetValue(__instance, false);

        // 2) 把 icon 替换为正常解锁状态贴图（原版 Init 在 _isLocked=true 分支会贴 LockedIcon）
        if (IconField?.GetValue(__instance) is TextureRect icon)
        {
            icon.Texture = character.CharacterSelectIcon;
        }

        // 3) 隐藏锁图标（原版会在 locked 分支把 _lock.Visible 设为 true）
        if (LockField?.GetValue(__instance) is Control lockNode)
        {
            lockNode.Visible = false;
        }
    }
}
