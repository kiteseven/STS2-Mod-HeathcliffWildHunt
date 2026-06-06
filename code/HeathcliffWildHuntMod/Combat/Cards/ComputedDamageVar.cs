using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HeathcliffWildHuntMod.Combat.Cards;

/// <summary>
/// 计算型伤害变量：卡面 <c>{Damage}</c> 显示的值由委托按「当前卡牌 + 当前指向目标」实时算出，
/// 而不是固定的基础伤害。用于解决「卡面显示固定数字、实战伤害随目标/自身状态浮动」的脱节问题。
///
/// 设计借鉴 RitsuLib 的 ComputedDynamicVar（委托驱动），但按本项目约定自己实现、不引用前置库。
///
/// 工作方式：
///   - <see cref="DynamicVar.BaseValue"/> 仍是卡牌原始基础伤害（升级会改它），<c>OnPlay</c> 读它不受影响；
///   - 引擎在玩家指向/悬停敌人时调用 <see cref="UpdateCardPreview"/>，本类用 <c>_effectiveBase</c> 委托
///     算出「含本卡全部自定义加成的有效基础伤害」，再跑 vanilla 增伤/减伤 hook（力量、易伤等），
///     写入 <see cref="DynamicVar.PreviewValue"/> —— 卡面据此刷新，口径与实战 DamageCmd.Attack 一致。
/// </summary>
public sealed class ComputedDamageVar : DamageVar
{
    /// <summary>
    /// 算「预 hook 的有效基础伤害」。入参为当前卡牌与当前目标（目标可为 null，表示未指向任何敌人）。
    /// 委托内部需对「无战斗上下文 / canonical 实例」容错——本类在调用前已挡掉这些情况。
    /// </summary>
    private readonly Func<CardModel, Creature?, decimal> _effectiveBase;

    public ComputedDamageVar(decimal baseDamage, ValueProp props,
        Func<CardModel, Creature?, decimal> effectiveBase)
        : base(baseDamage, props)
    {
        ArgumentNullException.ThrowIfNull(effectiveBase);
        _effectiveBase = effectiveBase;
    }

    public override void UpdateCardPreview(
        CardModel card, CardPreviewMode previewMode, Creature? target, bool runGlobalHooks)
    {
        // 无任何上下文（图鉴 / canonical 实例）：保持基础伤害，别让委托访问 Owner 抛 CanonicalModelException。
        if (card.RunState == null && card.CombatState == null)
        {
            PreviewValue = BaseValue;
            return;
        }

        decimal effectiveBase = _effectiveBase(card, target);

        // 与实战同口径地跑增伤 hook（仅在有战斗状态、且引擎要求跑全局 hook 时）。
        if (runGlobalHooks && card.CombatState != null)
        {
            effectiveBase = Hook.ModifyDamage(
                card.Owner.RunState, card.CombatState, target, card.Owner.Creature,
                effectiveBase, Props, card, ModifyDamageHookType.All, previewMode,
                out IEnumerable<AbstractModel> _);
        }

        PreviewValue = Math.Max(effectiveBase, 0m);
    }
}
