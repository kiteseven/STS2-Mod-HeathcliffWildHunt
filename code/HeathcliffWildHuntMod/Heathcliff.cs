using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.PotionPools;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.Combat;
using HeathcliffWildHuntMod.Powers;
using HeathcliffWildHuntMod.Relics;
using MegaCrit.Sts2.Core.Models.RelicPools;
using HeathcliffWildHuntMod.RelicPools;
using HeathcliffWildHuntMod.Cards;
using HeathcliffWildHuntMod.Visuals;
using HeathcliffWildHuntMod.Visuals.Definition;
using HeathcliffWildHuntMod.Audio;

namespace HeathcliffWildHuntMod;

public sealed class Heathcliff : CharacterModel, IModCharacterVisualCues, IDullahanFormVisuals
{
	public const string EnergyColorName = "wild_hunt";

	/// <summary>狂猎专属能量表盘场景路径。Ryoshu 等 mod 用同样的 new 模式覆盖基类计算路径。</summary>
	public new string EnergyCounterPath => "res://scenes/combat/energy_counters/heathcliff_energy_counter.tscn";

	public override CharacterGender Gender => CharacterGender.Masculine;

	/// <summary>
	/// 开发期始终解锁：返回 null 表示无前置解锁条件，新存档也能直接在选人界面点选本角色。
	/// 原版逻辑（参考 <see cref="MegaCrit.Sts2.Core.Models.Characters.Silent"/> 等）会要求先用解锁前置角色通关一次，对 mod 调试很不友好。
	/// 正式发布时若想接入官方解锁链，可改回 <c>ModelDb.Character&lt;Ironclad&gt;()</c>。
	/// </summary>
	protected override CharacterModel? UnlocksAfterRunAs => null;

	public override Color NameColor => new Color("3A0B80");

	public override int StartingHp => 72;

	public override int StartingGold => 99;

	public override CardPoolModel CardPool => ModelDb.CardPool<HeathcliffCardPool>();

	public override RelicPoolModel RelicPool => ModelDb.RelicPool<HeathcliffRelicPool>();

	public override PotionPoolModel PotionPool => ModelDb.PotionPool<SharedPotionPool>();

	/// <summary>
	/// 起始卡组：6 斩首 + 3 忍耐 + 1 空棺 + 1 杜拉罕啊…… + 1 把你也装进这棺材。
	/// 与 docx v2 设计案 Table 6（B1-B5）保持一致。
	/// </summary>
	public override IEnumerable<CardModel> StartingDeck => new List<CardModel>
	{
		// B1 斩首 ×6：基础攻击主力
		ModelDb.Card<Decapitate>(),
		ModelDb.Card<Decapitate>(),
		ModelDb.Card<Decapitate>(),
		ModelDb.Card<Decapitate>(),
		ModelDb.Card<Decapitate>(),
		ModelDb.Card<Decapitate>(),
		// B2 忍耐 ×3：基础格挡
		ModelDb.Card<Endure>(),
		ModelDb.Card<Endure>(),
		ModelDb.Card<Endure>(),
		// B3 空棺 ×1：棺资源入口
		ModelDb.Card<EmptyCoffin>(),
		// B4 杜拉罕啊……！ ×1：理智阈值触发杜拉罕层数
		ModelDb.Card<OhDullahan>(),
		// B5 把你也装进这棺材 ×1：panic 检测攻击
		ModelDb.Card<IntoThisCoffin>(),
		// 初始 EGO 裹尸袋 ×1：开局携带；常驻 Deck 随存档持久化，
		// 战斗建堆时由 PatchPopulateCombatStateRouteEgo 分流进 EGO 专属堆（不进抽牌循环）。
		ModelDb.Card<Bodysack>(),
	};

	public override IReadOnlyList<RelicModel> StartingRelics => new List<RelicModel>
		{
			ModelDb.Relic<CatherineCoffinRelic>(),
	};

	public override float AttackAnimDelay => 0.18f;

	public override float CastAnimDelay => 0.25f;

	/// <summary>
	/// 选人界面点选音效：返回原版风格路径，由 PatchNAudioManagerModEvents 拦截并重定向到 mod 的 select 音效文件。
	/// （音频改为裸文件加载，不走 bank/GUID；这条路径只作为"触发标记"被 patch 捕获。）
	/// </summary>
	public override string CharacterSelectSfx => "event:/sfx/characters/heathcliff/heathcliff_select";

	/// <summary>
	/// embark 过场擦屏音效：暂时复用铁甲的 wipe，避免
	/// <c>cannot find sfx path: event:/sfx/ui/wipe_heathcliff</c>。
	/// </summary>
	public override string CharacterTransitionSfx => "";

	public override Color EnergyLabelOutlineColor => new Color("3A0B80FF");

	public override Color DialogueColor => new Color("0B0F18");

	public override Color MapDrawingColor => new Color("3A0B80");

	public override Color RemoteTargetingLineColor => new Color("7B4FBFFF");

	public override Color RemoteTargetingLineOutline => new Color("0B0F18FF");

	public override List<string> GetArchitectAttackVfx() => new()
	{
		"vfx/vfx_attack_slash",
		"vfx/vfx_heavy_blunt",
		"vfx/vfx_bloody_impact",
	};

	public override CreatureAnimator GenerateAnimator(MegaSprite controller)
	{
		// 角色已切换为 Spine 骨骼动画：走 vanilla CreatureAnimator 流程。
		// NCreature 在 HasSpineAnimation==true 时调用本方法建立 animator，所有 SetAnimationTrigger 都经它驱动。
		// 动画名与 vanilla 契约一致（idle_loop / attack / hurt / dead / block ...），并带杜拉罕形态 -d 变体。

		// ── 默认形态状态 ──（一次性动画播完回 idle_loop）
		var idle    = new AnimState("idle_loop", isLooping: true);
		var attack  = new AnimState("attack")  { NextState = idle };
		var attack2 = new AnimState("attack2") { NextState = idle };
		var attack3 = new AnimState("attack3") { NextState = idle };
		var hurt    = new AnimState("hurt")    { NextState = idle };
		var block   = new AnimState("block")   { NextState = idle };
		var dead    = new AnimState("dead");   // 死亡不回 idle

		// ── 杜拉罕形态状态（-d 变体）──（回 idle_loop-d 维持形态外观）
		var idleD    = new AnimState("idle_loop-d", isLooping: true);
		var attackD  = new AnimState("attack-d")  { NextState = idleD };
		var attack2D = new AnimState("attack2-d") { NextState = idleD };
		var attack3D = new AnimState("attack3-d") { NextState = idleD };
		var hurtD    = new AnimState("hurt-d")    { NextState = idleD };
		var blockD   = new AnimState("block-d")   { NextState = idleD };
		// 变身演出：DullahanPower 0→1 时由 SetAnimationTrigger("RideStart") 触发，播完转入杜拉罕 idle。
		var rideStart = new AnimState("ride-start-dullahan") { NextState = idleD };
		// 杜拉罕形态没有独立 dead 资源 → 复用默认形态的 dead。block 已有独立 block-d。

		// 杜拉罕形态判定：直接读"被本 animator 驱动的这只 creature 自身"的 DullahanPower 层数。
		// ⚠️ 不能用 LocalContext.GetMe(全局本地玩家)——敌人回合触发 Hit 时该上下文不可靠，
		//    会误判成非杜拉罕形态，导致受击后回到默认 idle（本 bug 的根因）。
		//    controller.BoundObject 是 Spine 的 Visuals 节点，其父节点就是这只 NCreature。
		bool InDullahan() => CreatureInDullahanForm(controller);

		// 初始状态：进战斗时按当前形态决定 idle 变体（一般是默认形态）。
		var initial = InDullahan() ? idleD : idle;
		var anim = new CreatureAnimator(initial, controller);

		// AnyState 分支：每个 trigger 先注册"杜拉罕变体"分支（condition=形态中），
		// 再注册普通分支（condition=null 兜底）。CallTrigger 取首个 condition 通过的分支。
		anim.AddAnyState("Idle",   idleD,    InDullahan);
		anim.AddAnyState("Idle",   idle);
		anim.AddAnyState("Attack", attackD,  InDullahan);
		anim.AddAnyState("Attack", attack);
		anim.AddAnyState("Attack2", attack2D, InDullahan);
		anim.AddAnyState("Attack2", attack2);
		anim.AddAnyState("Attack3", attack3D, InDullahan);
		anim.AddAnyState("Attack3", attack3);
		anim.AddAnyState("Hit",    hurtD,    InDullahan);
		anim.AddAnyState("Hit",    hurt);
		// Cast：Spine 无 cast 动画 → 复用攻击动画，避免 "could not find 'cast'" 警告。
		anim.AddAnyState("Cast",   attackD,  InDullahan);
		anim.AddAnyState("Cast",   attack);
		// Block：两形态分流（杜拉罕用 block-d）。
		anim.AddAnyState("Block",  blockD, InDullahan);
		anim.AddAnyState("Block",  block);
		// Dead：两形态共用同一资源。
		anim.AddAnyState("Dead",   dead);
		// 杜拉罕变身专用 trigger。
		anim.AddAnyState("RideStart", rideStart);
		return anim;
	}

	/// <summary>
	/// 被给定 Spine controller 驱动的那只 creature 当前是否处于杜拉罕形态（DullahanPower &gt; 0）。
	/// 供 <see cref="GenerateAnimator"/> 的 AnyState condition 闭包使用。
	/// <para>
	/// 直接从 controller 反查到 creature 自身，而非依赖全局本地玩家上下文：
	/// <c>controller.BoundObject</c>（Spine 的 %Visuals 节点）向上遍历到 <see cref="NCreature"/> → <c>Entity</c>。
	/// 节点层级是 NCreature → NCreatureVisuals → %Visuals(SpineSprite)，所以 BoundObject 的父节点是
	/// NCreatureVisuals、祖父才是 NCreature；这里用循环向上找，不写死层数，避免 vanilla 包装层变动时失效。
	/// 这样在敌人回合（受击触发 Hit）也能正确读到本角色的杜拉罕层数，避免形态误判。
	/// </para>
	/// </summary>
	private static bool CreatureInDullahanForm(MegaSprite controller)
	{
		if (controller.BoundObject is not Node node) return false;
		// 从 Spine 节点向上遍历，找到承载本 creature 的 NCreature。
		for (Node? n = node; n != null; n = n.GetParent())
		{
			if (n is NCreature creatureNode)
				return creatureNode.Entity?.GetPowerAmount<DullahanPower>() > 0;
		}
		return false;
	}

	/// <summary>
	/// 默认形态 cue 集合：覆盖 idle/cast/hurt/die + attack/attack2/attack3 多帧序列。
	/// 资源在 <c>res://animations/characters/Heathcliff-WildHunt/</c> 下，路径在 <see cref="HeathcliffVisualCues"/> 集中维护。
	/// </summary>
	public VisualCueSet? VisualCues => HeathcliffVisualCues.Default;

	/// <summary>
	/// 杜拉罕形态 cue 集合：玩家身上 DullahanPower &gt; 0 时由 dispatcher 自动切到这里，
	/// 多了 ride_start 变身演出 + 全部 -d 后缀贴图。
	/// </summary>
	public VisualCueSet? DullahanVisualCues => HeathcliffVisualCues.Dullahan;
}
