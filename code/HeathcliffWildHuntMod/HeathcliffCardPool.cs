using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using HeathcliffWildHuntMod.Cards;

namespace HeathcliffWildHuntMod;

/// <summary>
/// 狂猎希斯克利夫的卡池声明：注册角色专属基础卡 / 普通卡 / 罕见卡 / 稀有卡 / EGO。
/// 当前阶段只装载 5 张基础卡（B1-B5），其余卡按设计文档分期加入。
/// </summary>
public sealed class HeathcliffCardPool : CardPoolModel
{
    /// <summary>卡池标识，用于本地化 / 图鉴检索。</summary>
    public override string Title => "wild_hunt";

    /// <summary>能量配色：暂用 colorless，等 energy_wild_hunt 精灵图进 PCK 后再切回 wild_hunt。</summary>
    public override string EnergyColorName => "colorless";

    /// <summary>卡框材质：沿用诅咒框（紫色基调），再通过 <see cref="PatchCardFrameColors"/> 叠加 SelfModulate 微调。</summary>
    public override string CardFrameMaterialPath => "card_frame_curse";

    /// <summary>图鉴里该角色卡的默认底色：狂猎主题深紫 #3A0B80。</summary>
    public override Color DeckEntryCardColor => new Color("3A0B80FF");

    /// <summary>能量费用数字外描边色：狂猎深紫 #3A0B80。</summary>
    public override Color EnergyOutlineColor => new Color("3A0B80FF");

    /// <summary>本卡池属于角色专属池，不参与无色卡共享。</summary>
    public override bool IsColorless => false;

    /// <summary>当前注册的全部卡牌实例。</summary>
    protected override CardModel[] GenerateAllCards() => new CardModel[]
    {
        // ── 基础卡 (5) ──
        ModelDb.Card<Decapitate>(),
        ModelDb.Card<Endure>(),
        ModelDb.Card<EmptyCoffin>(),
        ModelDb.Card<OhDullahan>(),
        ModelDb.Card<IntoThisCoffin>(),
        // ── 衍生卡 ──
        ModelDb.Card<Lament>(),
        // ── 普通卡 (5) ──
        ModelDb.Card<CoffinNail>(),
        ModelDb.Card<WildSwing>(),
        ModelDb.Card<VentAnger>(),
        ModelDb.Card<FuneralProcession>(),
        ModelDb.Card<RevengeHeart>(),
        ModelDb.Card<CoffinBarrier>(),
        ModelDb.Card<SelfLoathing>(),
        ModelDb.Card<Numbness>(),
        ModelDb.Card<BlackCloak>(),
        ModelDb.Card<GraveGuard>(),
        ModelDb.Card<WastelandDash>(),
        ModelDb.Card<SadWhisper>(),
        ModelDb.Card<ManorWhisper>(),
        ModelDb.Card<FuneralMemory>(),
        ModelDb.Card<FuneralStep>(),
        ModelDb.Card<ThoughtGather>(),
        ModelDb.Card<ClubStrike>(),
        ModelDb.Card<CoffinHammer>(),
        ModelDb.Card<RainDash>(),
        ModelDb.Card<WolfSweep>(),
        ModelDb.Card<AtoneForSin>(),
        ModelDb.Card<DeathStance>(),
        ModelDb.Card<DecapitateBlade>(),
        ModelDb.Card<PutDownGrudge>(),
        ModelDb.Card<SpillAgony>(),
        ModelDb.Card<Knockout>(),
        // ── 罕见卡 (8) ──
        ModelDb.Card<RainHunt>(),
        ModelDb.Card<TornHeart>(),
        ModelDb.Card<WildHuntCharge>(),
        ModelDb.Card<AllHeathcliff>(),
        ModelDb.Card<EndlessRoad>(),
        ModelDb.Card<CatherineWish>(),
        ModelDb.Card<WildernessStorm>(),
        ModelDb.Card<RuiningWrath>(),
        ModelDb.Card<SinkingThrust>(),
        ModelDb.Card<CoffinWater>(),
        ModelDb.Card<CoffinWhisper>(),
        ModelDb.Card<CoffinOath>(),
        ModelDb.Card<Decapitator>(),
        ModelDb.Card<SummonIshmael>(),
        ModelDb.Card<SummonFaust>(),
        ModelDb.Card<SummonOutis>(),
        ModelDb.Card<SummonRyoshu>(),
        ModelDb.Card<SummonGregor>(),
        ModelDb.Card<Silence>(),
        // ── 稀有卡 (8) ──
        ModelDb.Card<RunToDeathDullahan>(),
        ModelDb.Card<WildHuntDescent>(),
        ModelDb.Card<NightmareAvengerReturns>(),
        ModelDb.Card<FadedPromise>(),
        ModelDb.Card<LoveAndHate>(),
        ModelDb.Card<DecapitateHeathcliffCard>(),
        ModelDb.Card<MourningWall>(),
        ModelDb.Card<SpilledAgonyGlassShards>(),
        ModelDb.Card<Aedd>(),
        ModelDb.Card<CoffinBurst>(),
        ModelDb.Card<ObsessionReturn>(),
        ModelDb.Card<CoffinSearch>(),
        ModelDb.Card<FuneralSilence>(),
        ModelDb.Card<RuinTombstone>(),
        ModelDb.Card<FuneralMass>(),
        ModelDb.Card<CoffinCollapse>(),
        ModelDb.Card<MourningKing>(),
        ModelDb.Card<WildHuntKing>(),
        ModelDb.Card<GrudgeFire>(),
        ModelDb.Card<DemonKingsPath>(),
        ModelDb.Card<GlassShards>(),
        ModelDb.Card<Requiem>(),
        // 裹尸袋(Bodysack)：初始 EGO 卡。虽不进普通奖励池（靠自身 Ancient 罕见度排除），
        // 但<b>必须登记进卡池</b>——它在 StartingDeck 常驻，vanilla 的 CardModel.Pool getter 会回退到
        // MockCardPool 并对"不在任何真实池"的卡抛 "You monster!"，进而在卡牌盒渲染 PortraitPath 时崩溃
        // （表现为打开卡牌盒全空白）。登记后 Pool 能正常解析，卡牌盒恢复显示。
        ModelDb.Card<Bodysack>(),
        // ── 追加 EGO 卡 (8) ──
        // 与 Bodysack 同理：EGO 卡靠 Ancient 稀有度排除普通奖励池，但<b>必须登记</b>否则
        // Pool getter 回退 MockCardPool 抛 "You monster!" 导致卡牌盒崩溃。
        ModelDb.Card<Holiday>(),
        ModelDb.Card<AeddEgo>(),
        ModelDb.Card<FellBullet>(),
        ModelDb.Card<MoveInReg>(),
        ModelDb.Card<Telepole>(),
        ModelDb.Card<Sunyata>(),
        ModelDb.Card<AsymmetricalInertia>(),
        ModelDb.Card<Binds>(),
        ModelDb.Card<FuneralParade>(),
        // ── 先古卡 (Ancient，不进普通池，仅先古事件发放) ──
        // 觉悟：达弗(Darv)的尘封魔典随机给。镇魂曲(Requiem)虽也是 Ancient，但走欧洛巴斯升华，
        //       已被 ArchaicTooth.TranscendenceCards 从达弗池排除（见 PatchArchaicToothTranscendence）。
        ModelDb.Card<Resolve>(),
        // ── 额外 ──
        ModelDb.Card<SanityForge>(),
        // ── 消耗（烧牌）archetype 样例 ──
        ModelDb.Card<PyreOffering>(),
        ModelDb.Card<AshFrenzy>(),
    };
}
