namespace CustomBossChallenge.Config;

/// <summary>
/// How the skull thresholds of the boss should be determined
/// </summary>
public enum SkullMode
{
    /// <summary>Keep the skulls the boss normally has</summary>
    Vanilla,

    /// <summary>Place <see cref="BossChallengeConfig.SkullCount"/> skulls evenly across the health bar</summary>
    EvenlySpaced,

    /// <summary>Use the comma separated percentages in <see cref="BossChallengeConfig.CustomSkulls"/></summary>
    Custom
}

/// <summary>
/// Plain snapshot of every customizable value. Percentages are whole numbers (100 = unchanged),
/// and start rules use 0 to mean "keep the vanilla value".
/// </summary>
public sealed record BossChallengeConfig
{
    public int BossSpeed { get; init; } = 100;
    public int BossHealth { get; init; } = 100;

    public int BloonSpeed { get; init; } = 100;
    public int MoabSpeed { get; init; } = 100;
    public int CeramicHealth { get; init; } = 100;
    public int MoabHealth { get; init; } = 100;
    public int RegrowRate { get; init; } = 100;

    public SkullMode SkullMode { get; init; } = SkullMode.Vanilla;
    public int SkullCount { get; init; } = 3;
    public string CustomSkulls { get; init; } = "75,50,25";

    public int StartCash { get; init; }
    public int StartLives { get; init; }
    public int MaxLives { get; init; }
    public int StartRound { get; init; }
    public int EndRound { get; init; }

    public bool NoMonkeyKnowledge { get; init; }
    public bool NoSelling { get; init; }
    public bool NoPowers { get; init; }
    public bool NoContinues { get; init; }
    public bool AllCamo { get; init; }
    public bool AllRegen { get; init; }
    public bool AllFortified { get; init; }

    /// <summary>
    /// The skull thresholds (0-1, descending) this config asks for, or null to keep the vanilla skulls
    /// </summary>
    public float[]? SkullPercents => SkullMode switch
    {
        SkullMode.EvenlySpaced => SkullMath.EvenlySpaced(SkullCount),
        SkullMode.Custom => SkullMath.ParseCustom(CustomSkulls),
        _ => null
    };
}
