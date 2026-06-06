using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace HeathcliffWildHuntMod.Monsters;

/// <summary>狂猎随从基类——玩家侧召唤物，只 idle 不主动行动。</summary>
public abstract class WildHuntMinionModel : MonsterModel
{
    /// <summary>卡牌是否已升级——由 MinionCmdHelper 在 OnSummon 之前设置。</summary>
    public bool IsUpgraded { get; set; }

    /// <summary>召唤后触发一次（应用永久 buff、记录到 HeathcliffStatePower 等）。此时 <see cref="IsUpgraded"/> 已正确设置。</summary>
    public virtual Task OnSummon(Player owner, Creature self, CardModel? source)
        => Task.CompletedTask;

    /// <summary>随从不主动移动——只有一个 IDLE 状态循环。</summary>
    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var idle = new MoveState("MINION_IDLE", _ => Task.CompletedTask);
        idle.FollowUpState = idle;
        return new MonsterMoveStateMachine(new[] { idle }, idle);
    }
}
