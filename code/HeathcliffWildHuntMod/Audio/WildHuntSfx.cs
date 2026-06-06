namespace HeathcliffWildHuntMod.Audio;

/// <summary>
///     狂猎 mod 全部音频文件路径的唯一真源（single source of truth）。
///     裸文件直接加载方案（FMOD load_file_as_sound + create_sound_instance），不走 bank/GUID。
///
///     文件要求：wav 须 Godot "Keep File (No Import)" 模式（importer="keep"），裸文件进 PCK 并可被 FileAccess 读取。
///     全部位于 res://audio/heathcliff/。
///
///     【系列模型】攻击音效按"系列"组织：一个系列 = 一组要 <b>顺序连播</b> 的片段（同一次攻击把该系列依次播完）。
///     一次攻击从对应"池"里随机挑一个系列连播。多个系列组成池，实现随机 + 连播。
/// </summary>
internal static class WildHuntSfx
{
    /// <summary>音频文件目录前缀。</summary>
    public const string Dir = "res://audio/heathcliff/";

    private static string P(string file) => Dir + file;

    // ===== 攻击池：每个池是 string[][]，外层=可选系列（随机一个），内层=该系列顺序连播的片段 =====

    /// <summary>基础攻击池（trigger=Attack）。系列1：attack_1_1；系列2：attack_1_2-1→attack_1_2-2。</summary>
    public static readonly string[][] AttackBasicPool =
    {
        new[] { Dir + "attack_1_1.wav" },
        new[] { Dir + "attack_1_2-1.wav", Dir + "attack_1_2-2.wav" },
    };

    /// <summary>中级攻击池（trigger=Attack2）。系列：attack_2_3。</summary>
    public static readonly string[][] AttackMidPool =
    {
        new[] { Dir + "attack_2_3.wav" },
    };

    /// <summary>重型攻击池（trigger=Attack3/Attack4，3 与 4 合并随机）。三个系列各自顺序连播。</summary>
    public static readonly string[][] AttackHeavyPool =
    {
        new[] { Dir + "attack_3_1-1.wav", Dir + "attack_3_1-2.wav", Dir + "attack_3_1-3.wav", Dir + "attack_3_1-4.wav" },
        new[] { Dir + "attack_3_2-1.wav", Dir + "attack_3_2-2.wav", Dir + "attack_3_2-3.wav", Dir + "attack_3_2-4.wav", Dir + "attack_3_2-5.wav" },
        new[] { Dir + "attack_4_1-1.wav", Dir + "attack_4_1-2.wav", Dir + "attack_4_1-3.wav", Dir + "attack_4_1-4.wav" },
    };

    // ===== 单发音效（随机池：每次随机挑一个文件播；多数只有一个）=====

    /// <summary>施法/能力牌打出（cast）。</summary>
    public static readonly string[] Cast = { Dir + "cast.wav" };

    /// <summary>登场（heathcliff_entry）。</summary>
    public static readonly string[] Entry = { Dir + "heathcliff_entry.wav" };

    /// <summary>受伤（heathcliff_hurt）。</summary>
    public static readonly string[] Hurt = { Dir + "heathcliff_hurt.wav" };

    /// <summary>死亡（heathcliff_dead）。</summary>
    public static readonly string[] Dead = { Dir + "heathcliff_dead.wav" };

    /// <summary>战败/退场（heathcliff_defeat）。</summary>
    public static readonly string[] Defeat = { Dir + "heathcliff_defeat.wav" };

    /// <summary>进入杜拉罕形态（三选一随机：heathcliff_ride-start-dullahan_1..3）。</summary>
    public static readonly string[] RideDullahan =
    {
        Dir + "heathcliff_ride-start-dullahan_1.wav",
        Dir + "heathcliff_ride-start-dullahan_2.wav",
        Dir + "heathcliff_ride-start-dullahan_3.wav",
    };

    /// <summary>角色选择台词（heathcliff_select_line1）。</summary>
    public static readonly string[] Select = { Dir + "heathcliff_select_line1.wav" };

    // ===== 音乐 =====
    /// <summary>角色专属 BGM（循环；heathcliff-bgm）。后续可转 ogg 减小体积。</summary>
    public const string Bgm = Dir + "heathcliff-bgm.wav";

    /// <summary>所有需要预加载的攻击池（供服务展开预载）。</summary>
    public static readonly string[][][] AllAttackPools =
    {
        AttackBasicPool, AttackMidPool, AttackHeavyPool,
    };

    /// <summary>所有需要预加载的单发音效组。</summary>
    public static readonly string[][] AllSingleGroups =
    {
        Cast, Entry, Hurt, Dead, Defeat, RideDullahan, Select,
    };
}
