using CustomBossChallenge.Config;
using Il2CppAssets.Scripts.Models.ServerEvents;

namespace CustomBossChallenge.Creator;

/// <summary>
/// The last custom game started: its setup and the bosses it was played with, so a game with Random tiers can be
/// played again, and shared, with the bosses Random picked (#270)
/// </summary>
public static class LastGame
{
    /// <summary>
    /// The boss options in the Boss Creator when the game was started, Random tiers and all, or null for a game that
    /// was continued
    /// </summary>
    public static BossOptions? Setup { get; private set; }

    /// <summary>
    /// The options the game was played with: Random tiers have their bosses, and RandomTiers says which they were
    /// </summary>
    public static BossOptions? Played { get; private set; }

    /// <summary>
    /// A copy of the editor's challenge the game was made from, for its share code
    /// </summary>
    public static DailyChallengeModel? Dcm { get; private set; }

    public static void Remember(BossOptions? setup, BossOptions played, DailyChallengeModel editorDcm)
    {
        Setup = setup;
        Played = played;
        Dcm = editorDcm.Clone();
    }

    public static void Clear()
    {
        Setup = null;
        Played = null;
        Dcm = null;
    }

    /// <summary>
    /// Whether the Boss Creator offers to play <paramref name="current"/> again with the last game's bosses: it is the
    /// setup that game was started from, and that setup had Random tiers
    /// </summary>
    public static bool CanReplay(BossOptions current) =>
        Played != null && Setup != null && Setup.HasRandom && Setup == current;

    /// <summary>
    /// The options and challenge of a code with the bosses <paramref name="active"/>, the game being played, has: only
    /// for a game with tiers that were Random, started from this session's Boss Creator or Continue
    /// </summary>
    public static (BossOptions Options, DailyChallengeModel Dcm)? Lineup(BossOptions? active) =>
        active?.RandomTiers != null && active == Played && Dcm != null ? (active, Dcm) : null;
}
