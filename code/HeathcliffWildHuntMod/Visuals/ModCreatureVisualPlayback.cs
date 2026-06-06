using System;
using System.Collections.Generic;
using Godot;
using HeathcliffWildHuntMod.Powers;
using HeathcliffWildHuntMod.Visuals.Definition;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace HeathcliffWildHuntMod.Visuals;

/// <summary>
/// 【动画职责已停用】战斗场景下 cue 视觉的集中调度器。唯一入口
/// <c>PatchNCreatureSetAnimationTrigger</c> 已摘除（角色转 Spine），故本类当前没有活调用方。
/// 保留其帧序列/静态切图调度逻辑，作为将来「cue → 特效播放」改造的基础（特效挂独立节点，不碰角色 Spine 本体）。
/// <para>
/// 原职责：把 STS2 的 <c>SetAnimationTrigger</c> 字符串映射到 cue 键（idle/attack/hurt/die/cast/revive/stun），
/// 在没有 Spine 时从 <see cref="IModCharacterVisualCues.VisualCues"/> 找匹配 cue 应用到 NCreatureVisuals 下的主 Sprite2D，
/// 帧序列优先 / 静态贴图回退。
/// </para>
/// </summary>
public static class ModCreatureVisualPlayback
{
    // 各 trigger 的别名集合，按 RitsuLib 的命名习惯保留：原版 Trigger 大写，cue 文件名大概率小写
    private static readonly string[] DieCueNames = ["die", "death", "dead", "Dead"];
    private static readonly string[] IdleCueNames = ["idle", "Idle", "relaxed_loop"];
    private static readonly string[] HurtCueNames = ["hurt", "hit", "Hit"];
    private static readonly string[] AttackCueNames = ["attack", "Attack"];
    private static readonly string[] CastCueNames = ["cast", "Cast"];
    private static readonly string[] ReviveCueNames = ["revive", "Revive"];
    private static readonly string[] StunCueNames = ["stun", "Stun"];
    // 本 mod 自定义：完全阻挡播 block(miss.png)
    private static readonly string[] BlockCueNames = ["block", "Block", "miss"];
    // 本 mod 自定义：杜拉罕变身演出
    private static readonly string[] RideStartCueNames = ["ride_start", "Ride-Start-dullahan", "ride-start"];
    // 本 mod 自定义：多段攻击
    private static readonly string[] Attack2CueNames = ["attack2", "Attack2"];
    private static readonly string[] Attack3CueNames = ["attack3", "Attack3"];

    /// <summary>
    /// 在 <paramref name="visuals"/> 上播放 <paramref name="primaryCue"/> 对应的 cue。命中任意一层视觉返回 true。
    /// </summary>
    /// <param name="visuals">NCreature.Visuals 节点。</param>
    /// <param name="character">角色模型；用于读取 <see cref="IModCharacterVisualCues.VisualCues"/>。</param>
    /// <param name="primaryCue">主 cue 名（如 "die"）。</param>
    /// <param name="alternateCueNames">额外别名；为空时按 primaryCue 推默认别名集合。</param>
    public static bool TryPlayCue(NCreatureVisuals visuals, CharacterModel? character, string primaryCue,
        ReadOnlySpan<string> alternateCueNames = default)
    {
        if (!GodotObject.IsInstanceValid(visuals) || string.IsNullOrWhiteSpace(primaryCue)) return false;

        var names = alternateCueNames.Length > 0
            ? alternateCueNames
            : BuildDefaultAlternateNames(primaryCue);

        // 切 cue 前先停掉旧的帧序列推进，避免下面切静态贴图时被旧 sequence 覆盖
        CueFrameSequencePlayer.StopUnder(visuals);

        return TryApplyVisualCues(visuals, character, names);
    }

    /// <summary>
    /// Harmony patch 入口：根据 NCreature 当前 trigger 派发到 cue 播放。
    /// 有 Spine 时返回 false 让原版逻辑处理；否则尝试播 cue 并返回是否命中。
    /// 攻击 trigger 走特殊流程：贴 move 帧 → Tween 滑到敌人位置 → 播攻击序列 → 瞬移回原位。
    /// </summary>
    public static bool TryPlayFromCreatureAnimatorTrigger(NCreature creature, string trigger)
    {
        // Spine 角色走原版的 _spineAnimator.SetTrigger，本 mod 不接管
        if (creature.HasSpineAnimation) return false;

        var primary = MapAnimatorTriggerToCue(trigger);
        var character = creature.Entity?.Player?.Character;

        // 攻击类 trigger 走"突进→攻击→归位"流程
        if (IsAttackCue(primary))
        {
            return TryPlayAttackWithDash(creature, primary, character);
        }

        return TryPlayCue(creature.Visuals, character, primary);
    }

    /// <summary>识别攻击类 cue（attack / attack2 / attack3）。</summary>
    private static bool IsAttackCue(string cue)
    {
        return string.Equals(cue, "attack",  StringComparison.OrdinalIgnoreCase)
            || string.Equals(cue, "attack2", StringComparison.OrdinalIgnoreCase)
            || string.Equals(cue, "attack3", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>攻击时 Tween 滑向敌人 → 播攻击帧 → 瞬移回原位。</summary>
    private static bool TryPlayAttackWithDash(NCreature creature, string attackCue, CharacterModel? character)
    {
        var sprite = FindPrimarySprite2D(creature.Visuals);
        if (sprite == null) return false;

        // 传入 creature.Visuals 作为 context：ResolveActiveCues 会从它向上找到 NCreature，读本 creature 自身的杜拉罕层数。
        var cues = ResolveActiveCues(character, creature.Visuals);
        if (cues == null) return false;

        // ① 先停掉前一次攻击遗留的帧序列。StopAndReset 会把 sprite 归位到 _returnPosition
        //   （如果前一次设过 override）或 _initialSpritePos。这样 tween 从正确位置开始。
        CueFrameSequencePlayer.StopUnder(creature.Visuals);

        // ② 记录原位（播放完攻击序列后瞬移回这个位置）
        var originalPosition = sprite.Position;

        // ② 找目标敌人的 X 坐标（冲到敌人面前 70% 处，不过于贴近），Y 方向上挑 30px
        Vector2 targetLocal;
        var targetNode = FindTargetCreatureNode(creature);
        if (targetNode != null)
        {
            var parent = sprite.GetParent() as Node2D;
            float enemyLocalX = parent != null
                ? parent.ToLocal(targetNode.GlobalPosition).X
                : targetNode.GlobalPosition.X - sprite.GlobalPosition.X + sprite.Position.X;
            // 只冲到敌人位置的 70%（留出攻击距离，不要贴脸）
            float dashX = sprite.Position.X + (enemyLocalX - sprite.Position.X) * 0.70f;
            targetLocal = new Vector2(dashX, sprite.Position.Y - 30);
        }
        else
        {
            // 找不到目标时默认向右前方冲 140px（纯兜底）
            targetLocal = sprite.Position + new Vector2(140, -30);
        }

        // ③ 先贴"move"冲刺激帧（如果 cue set 里有），再创建 Tween 滑过去
        if (cues.TexturePathByCue is { Count: > 0 } texMap &&
            TryGetOrdinalIgnoreCase(texMap, "move", out var movePath) &&
            !string.IsNullOrWhiteSpace(movePath))
        {
            var moveTex = ResourceLoader.Load<Texture2D>(movePath);
            if (moveTex != null) sprite.Texture = moveTex;
        }

        // ④ Tween 滑到敌人面前
        var tree = creature.Visuals.GetTree();
        if (tree == null) return TryPlayCue(creature.Visuals, character, attackCue);

        // 只杀上一次为本 sprite 创建的滑步 Tween，不动全局其他 Tween（如 Power VFX）。
        // 之前用 tree.GetProcessedTweens() 会把 NPowerAppliedVfx / NPowerFlashVfx 等全局
        // VFX 动画一并杀掉，导致特效图片卡在半透明/错位状态。
        var oldDashTween = sprite.GetMeta("__attack_dash_tween");
        if (oldDashTween.VariantType != Variant.Type.Nil)
        {
            var old = oldDashTween.As<Tween>();
            if (old.IsValid() && old.IsRunning()) old.Kill();
        }

        var tween = tree.CreateTween();
        sprite.SetMeta("__attack_dash_tween", Variant.From(tween));
        tween.SetProcessMode(Tween.TweenProcessMode.Physics);
        // 链式 Tween：先滑位置 → 再切 attack 帧 + 设归位点
        tween.TweenProperty(sprite, "position", targetLocal, 0.28f);
        tween.TweenCallback(Callable.From(() =>
        {
            if (!GodotObject.IsInstanceValid(sprite)) return;
            var ok = TryApplyFrameSequences(creature.Visuals, cues, BuildDefaultAlternateNames(attackCue))
                  || TryApplySingleTextureCues(creature.Visuals, cues, BuildDefaultAlternateNames(attackCue));
            if (ok)
            {
                var player = creature.Visuals.GetNodeOrNull<CueFrameSequencePlayer>(CueFrameSequencePlayer.NodeName);
                player?.OverrideReturnPosition(originalPosition);
            }
        }));

        return true;
    }

    /// <summary>找个与 <paramref name="self"/> 敌对的 creature 节点作为攻击目标。找不到返回 null。</summary>
    private static NCreature? FindTargetCreatureNode(NCreature self)
    {
        var room = NCombatRoom.Instance;
        if (room == null) return null;
        var selfIsPlayer = self.Entity?.IsPlayer ?? false;
        foreach (var node in room.CreatureNodes)
        {
            if (node == self) continue;
            if (!GodotObject.IsInstanceValid(node)) continue;
            var isPlayer = node.Entity?.IsPlayer ?? false;
            // 找第一个不同阵营的：玩家找敌人、敌人找玩家
            if (isPlayer != selfIsPlayer) return node;
        }
        return null;
    }

    /// <summary>
    /// 给攻击序列的 Finished 加一层"先瞬移回原位、再切 idle"的逻辑。
    /// 在 <see cref="AttachIdleFallback"/> 之前介入，趁 sprite 纹理还没被 idle 覆盖、位置先回。
    /// </summary>
    private static void AttachAttackReturnPosition(Sprite2D sprite, Vector2 returnPosition)
    {
        // 直接在下一空闲帧重置位置——Finish 回调会在 idle fallback 之前触发，因为 AttachIdleFallback
        // 也是通过 player.Finished += handler 订阅的，同一个 Finished.Invoke() 调用栈最先注册的最先执行。
        // 为确保不论订阅顺序如何都能先还原位置，这里用 CallDeferred 把位置写回到下一帧——
        // 此时 idle fallback 已经设完 texture，但位置已经正确。
        var tree = sprite.GetTree();
        if (tree == null) return;
        var timer = tree.CreateTimer(0.0);
        timer.Timeout += () =>
        {
            if (GodotObject.IsInstanceValid(sprite))
            {
                sprite.Position = returnPosition;
            }
        };
    }

    /// <summary>
    /// 选出当前应使用的 cue 集合：当"正在被驱动的这只 creature 自身"的 <see cref="DullahanPower"/> 层数 &gt; 0，
    /// 且角色实现了 <see cref="IDullahanFormVisuals"/> 时，优先返回 dullahan 形态 cue；
    /// 否则回落到默认 <see cref="IModCharacterVisualCues.VisualCues"/>。
    /// </summary>
    /// <param name="character">角色模型，用于取 cue 集合。</param>
    /// <param name="creatureContext">本次播放所属的 creature 相关节点（NCreatureVisuals 或其子节点）；用于反查 creature 自身的杜拉罕层数。</param>
    private static VisualCueSet? ResolveActiveCues(CharacterModel character, Node? creatureContext)
    {
        // 形态 cue：仅当本 creature 自身 DullahanPower.Amount > 0 时才使用 -d 贴图。
        // ⚠️ 不能用 LocalContext.GetMe(全局本地玩家)——敌人回合触发 Hit 时该上下文不可靠，
        //    会误判成非杜拉罕形态，导致"受击后切回默认待机"（本 bug 的根因，与 Spine 路径同源）。
        if (character is IDullahanFormVisuals { DullahanVisualCues: { } dullahan } &&
            CreatureInDullahanForm(creatureContext))
        {
            return dullahan;
        }

        return (character as IModCharacterVisualCues)?.VisualCues;
    }

    /// <summary>
    /// 从给定节点（NCreatureVisuals 或其子/同级节点）向上遍历，找到承载本 creature 的 <see cref="NCreature"/>，
    /// 读它自身的 <see cref="DullahanPower"/> 层数判断是否处于杜拉罕形态。
    /// <para>
    /// 直接读"本 creature"而不是全局本地玩家：敌人回合（玩家受击触发 Hit）时
    /// <c>LocalContext.GetMe</c> 不可靠，会把杜拉罕玩家误判成常态形态。这里用循环向上找、不写死层数，
    /// 与 Spine 路径的 <c>Heathcliff.CreatureInDullahanForm</c> 保持同一套判定逻辑。
    /// </para>
    /// </summary>
    private static bool CreatureInDullahanForm(Node? context)
    {
        for (Node? n = context; n != null; n = n.GetParent())
        {
            if (n is NCreature creatureNode)
                return creatureNode.Entity?.GetPowerAmount<DullahanPower>() > 0;
        }
        return false;
    }

    /// <summary>把 STS2 trigger 常量翻译成 cue 键名。未识别的 trigger 转小写。</summary>
    private static string MapAnimatorTriggerToCue(string trigger)
    {
        return trigger switch
        {
            CreatureAnimator.idleTrigger => "idle",
            CreatureAnimator.attackTrigger => "attack",
            CreatureAnimator.castTrigger => "cast",
            CreatureAnimator.hitTrigger => "hurt",
            CreatureAnimator.deathTrigger => "die",
            CreatureAnimator.reviveTrigger => "revive",
            "Stun" or "stun" => "stun",
            // 本 mod 自定义：完全阻挡（由 [[SanityPower.AfterDamageReceived]] 在 BlockedDamage>0 时主动派发）
            "Block" or "block" => "block",
            // 本 mod 自定义：杜拉罕变身（DullahanPower 0->1 触发一次）
            "RideStart" or "ride_start" => "ride_start",
            // 本 mod 自定义：可由卡牌按需触发的 attack2 / attack3
            "Attack2" or "attack2" => "attack2",
            "Attack3" or "attack3" => "attack3",
            _ => trigger.ToLowerInvariant(),
        };
    }

    /// <summary>主 cue → 别名集合的固定表；未知 cue 退化为 [原名] 或 [原名, 小写]。</summary>
    private static ReadOnlySpan<string> BuildDefaultAlternateNames(string primaryCue)
    {
        return primaryCue.ToLowerInvariant() switch
        {
            "die" => DieCueNames,
            "idle" => IdleCueNames,
            "hurt" => HurtCueNames,
            "attack" => AttackCueNames,
            "cast" => CastCueNames,
            "revive" => ReviveCueNames,
            "stun" => StunCueNames,
            "block" => BlockCueNames,
            "ride_start" => RideStartCueNames,
            "attack2" => Attack2CueNames,
            "attack3" => Attack3CueNames,
            _ => TwoNameFallback(primaryCue),
        };
    }

    private static string[] TwoNameFallback(string primaryCue)
    {
        var lower = primaryCue.ToLowerInvariant();
        // 小写已等于原名时只保留一份，避免别名重复扫
        return string.Equals(primaryCue, lower, StringComparison.Ordinal)
            ? [primaryCue]
            : [primaryCue, lower];
    }

    private static bool TryApplyVisualCues(Node visualsRoot, CharacterModel? character, ReadOnlySpan<string> names)
    {
        // 仅当角色实现 IModCharacterVisualCues 且返回非 null 时本 mod 才接管
        if (character == null) return false;
        // visualsRoot 即 NCreatureVisuals（或其子节点）：ResolveActiveCues 由它反查 NCreature 读本 creature 杜拉罕层数。
        var cues = ResolveActiveCues(character, visualsRoot);
        if (cues == null) return false;

        // 帧序列优先于单张贴图；同名 cue 不应同时存在两本字典里（builder 已互斥）
        return TryApplyFrameSequences(visualsRoot, cues, names)
               || TryApplySingleTextureCues(visualsRoot, cues, names);
    }

    private static bool TryApplyFrameSequences(Node visualsRoot, VisualCueSet cues, ReadOnlySpan<string> names)
    {
        var map = cues.FrameSequenceByCue;
        if (map == null || map.Count == 0) return false;

        var sprite = FindPrimarySprite2D(visualsRoot);
        if (sprite == null) return false;

        foreach (var name in names)
        {
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (!TryGetOrdinalIgnoreCase(map, name, out var sequence) || sequence is not { Frames.Count: > 0 })
                continue;

            var player = CueFrameSequencePlayer.EnsureUnder(visualsRoot);
            if (!player.TryStart(sprite, sequence)) return false;

            // 非循环序列（attack/cast/hurt 等）播完后必须自动回 idle：
            // vanilla Spine 状态机会自己 transition 回 idle，我们这里走 cue 不挂状态机，
            // 必须在 Finished 里手动接力，否则角色会停在攻击动画最后一帧（视觉上像"卡住只播一帧"）。
            if (!sequence.Loop)
            {
                AttachIdleFallback(player, visualsRoot, cues);
            }
            return true;
        }

        return false;
    }

    /// <summary>
    /// 给 player 挂一次性 Finished 订阅：序列播完后自动播 idle（若 cues 里有 idle）。
    /// 走 .NET event，订阅在回调内部解绑，避免重复触发；下一次 Play 时
    /// <see cref="CueFrameSequencePlayer.StopAndReset"/> 也会把残留订阅清掉作为兜底。
    /// </summary>
    private static void AttachIdleFallback(CueFrameSequencePlayer player, Node visualsRoot, VisualCueSet cues)
    {
        Action? handler = null;
        handler = () =>
        {
            // 解绑自己：handler 只跑一次
            if (handler != null) player.Finished -= handler;
            if (!Godot.GodotObject.IsInstanceValid(player) || !Godot.GodotObject.IsInstanceValid(visualsRoot)) return;

            // idle 优先用帧序列（如果以后做了 idle_loop 的多帧），否则回落到单张贴图
            var sprite = FindPrimarySprite2D(visualsRoot);
            if (sprite == null) return;

            // idle 帧序列
            if (cues.FrameSequenceByCue is { Count: > 0 } seqs)
            {
                foreach (var alt in IdleCueNames)
                {
                    if (TryGetOrdinalIgnoreCase(seqs, alt, out var seq) && seq is { Frames.Count: > 0 })
                    {
                        player.TryStart(sprite, seq);
                        return;
                    }
                }
            }

            // idle 静态贴图
            if (cues.TexturePathByCue is { Count: > 0 } texs)
            {
                foreach (var alt in IdleCueNames)
                {
                    if (!TryGetOrdinalIgnoreCase(texs, alt, out var path) || string.IsNullOrWhiteSpace(path)) continue;
                    var tex = Godot.ResourceLoader.Load<Godot.Texture2D>(path);
                    if (tex == null) continue;
                    sprite.Texture = tex;
                    if (cues.TextureStyleByCue is { Count: > 0 } styles &&
                        TryGetOrdinalIgnoreCase(styles, alt, out var style))
                    {
                        style.ApplyTo(sprite);
                    }
                    return;
                }
            }
        };
        player.Finished += handler;
    }

    private static bool TryApplySingleTextureCues(Node visualsRoot, VisualCueSet cues, ReadOnlySpan<string> names)
    {
        var map = cues.TexturePathByCue;
        if (map == null || map.Count == 0) return false;

        var sprite = FindPrimarySprite2D(visualsRoot);
        if (sprite == null) return false;

        foreach (var name in names)
        {
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (!TryGetOrdinalIgnoreCase(map, name, out var path) || string.IsNullOrWhiteSpace(path)) continue;

            // ResourceLoader 内部会缓存；同 path 重复加载不会反复 IO
            var tex = ResourceLoader.Load<Texture2D>(path);
            if (tex == null) continue;

            sprite.Texture = tex;
            // 静态 cue 可选样式：缩放 / 翻转 / 调色都按需写到 sprite 上
            if (cues.TextureStyleByCue is { Count: > 0 } styles &&
                TryGetOrdinalIgnoreCase(styles, name, out var style))
            {
                style.ApplyTo(sprite);
            }

            // 短暂态 cue（hurt/hit）需要在停留一会儿后自动回 idle，否则贴图永久卡住——
            // vanilla Spine 是状态机自动 transition 回 idle，我们走 cue 单帧贴图必须自己模拟。
            // die/cast/idle 不在此列：die 要保持死亡贴图、idle 本身就是循环、cast 通常由后续 trigger 切走。
            if (IsTransientSingleCue(name))
            {
                ScheduleAutoReturnToIdle(visualsRoot, cues);
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// 是否为"短暂态"单帧 cue（贴上后需要自动回 idle）。
    /// 当前：hurt/hit（受击）+ block（完全阻挡演出）；以后若加新瞬时单帧 cue 在这里加 case 即可。
    /// </summary>
    private static bool IsTransientSingleCue(string cueName)
    {
        return string.Equals(cueName, "hurt",  StringComparison.OrdinalIgnoreCase)
            || string.Equals(cueName, "hit",   StringComparison.OrdinalIgnoreCase)
            || string.Equals(cueName, "block", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>受击贴图持续多久后自动回 idle（秒）。值取小一点，跟 vanilla hit 反应同节奏。</summary>
    private const double TransientCueHoldSeconds = 0.35;

    /// <summary>
    /// 用 SceneTree.CreateTimer 在 <see cref="TransientCueHoldSeconds"/> 秒后把 Sprite2D 切回 idle。
    /// 走 SceneTreeTimer 是为了：(a) 不依赖我们自己的 ProcessFrame 订阅；(b) 跟 vanilla 暂停层级一致。
    /// </summary>
    private static void ScheduleAutoReturnToIdle(Node visualsRoot, VisualCueSet cues)
    {
        if (!Godot.GodotObject.IsInstanceValid(visualsRoot)) return;
        var tree = visualsRoot.GetTree();
        if (tree == null) return;

        // ignoreTimeScale=true 让暂停期间也照常回切（hurt 通常发生在战斗暂停帧期间）
        var timer = tree.CreateTimer(TransientCueHoldSeconds, processAlways: true);
        timer.Timeout += () =>
        {
            if (!Godot.GodotObject.IsInstanceValid(visualsRoot)) return;
            var sprite = FindPrimarySprite2D(visualsRoot);
            if (sprite == null) return;

            // 期间被新 cue 覆盖了（攻击/死亡）就不要再切——通过 CueFrameSequencePlayer.IsPlaying 判定。
            // 没在播帧序列时贴图就是上一次单帧 cue 留下的；这种情况下安全切回 idle。
            var existing = visualsRoot.GetNodeOrNull<CueFrameSequencePlayer>(CueFrameSequencePlayer.NodeName);
            if (existing != null && existing.IsPlaying) return;

            // idle 帧序列优先（如果将来做了 idle_loop 多帧），否则用单帧贴图
            if (cues.FrameSequenceByCue is { Count: > 0 } seqs)
            {
                foreach (var alt in IdleCueNames)
                {
                    if (TryGetOrdinalIgnoreCase(seqs, alt, out var seq) && seq is { Frames.Count: > 0 })
                    {
                        var player = CueFrameSequencePlayer.EnsureUnder(visualsRoot);
                        player.TryStart(sprite, seq);
                        return;
                    }
                }
            }
            if (cues.TexturePathByCue is { Count: > 0 } texs)
            {
                foreach (var alt in IdleCueNames)
                {
                    if (!TryGetOrdinalIgnoreCase(texs, alt, out var ipath) || string.IsNullOrWhiteSpace(ipath)) continue;
                    var idleTex = Godot.ResourceLoader.Load<Godot.Texture2D>(ipath);
                    if (idleTex == null) continue;
                    sprite.Texture = idleTex;
                    if (cues.TextureStyleByCue is { Count: > 0 } styles &&
                        TryGetOrdinalIgnoreCase(styles, alt, out var style))
                    {
                        style.ApplyTo(sprite);
                    }
                    return;
                }
            }
        };
    }

    /// <summary>找到挂在 visuals 节点下用来承载贴图的主 Sprite2D：%Visuals / Visuals 子节点优先，否则递归找。</summary>
    private static Sprite2D? FindPrimarySprite2D(Node root)
    {
        // 先按 RitsuLib 约定的命名查（"%Visuals" 是 unique-name 引用）
        var direct = root.GetNodeOrNull("%Visuals") ?? root.GetNodeOrNull("Visuals");
        if (direct is Sprite2D s) return s;

        if (root is Sprite2D rootSprite) return rootSprite;

        return SearchRecursive<Sprite2D>(root);
    }

    private static T? SearchRecursive<T>(Node parent) where T : class
    {
        foreach (var child in parent.GetChildren())
        {
            if (child is T match) return match;
            var found = SearchRecursive<T>(child);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>大小写不敏感字典查找：先精确，未命中再线性扫描。</summary>
    private static bool TryGetOrdinalIgnoreCase<TValue>(
        IReadOnlyDictionary<string, TValue> map, string key, out TValue? value)
    {
        if (map.TryGetValue(key, out var direct))
        {
            value = direct;
            return true;
        }

        foreach (var kv in map)
        {
            if (!string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase)) continue;
            value = kv.Value;
            return true;
        }

        value = default;
        return false;
    }
}
