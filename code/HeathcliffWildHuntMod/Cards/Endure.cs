using System.Collections.Generic;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace HeathcliffWildHuntMod.Cards;

/// <summary>
/// B2 忍耐 Endure：1 费技能。
/// 效果：获得 5 (8) 格挡。基础防御卡，不带任何资源 / debuff 操作。
/// </summary>
public sealed class Endure : CardModel
{
    // 基础格挡值（升级 +3）
    private const decimal BaseBlock = 5m;
    private const decimal UpgradeBlockBy = 3m;

    /// <summary>声明本卡会产生格挡，便于游戏意图系统识别。</summary>
    public override bool GainsBlock => true;

    protected override HashSet<CardTag> CanonicalTags => new() { CardTag.Defend };

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        new DynamicVar[] { new BlockVar(BaseBlock, ValueProp.Move) };

    public Endure() : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext ctx, CardPlay play)
    {
        // 给自己加格挡，敏捷由 STS2 内部 GainBlock 自动结算
        await CreatureCmd.GainBlock(base.Owner.Creature, DynamicVars.Block, play);
    }

    /// <summary>升级：格挡 +3。</summary>
    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(UpgradeBlockBy);
}
