using System;
using System.Collections.Generic;
using System.Linq;
using BTD_Mod_Helper;
using Il2CppAssets.Scripts.Simulation;
using Il2CppAssets.Scripts.Simulation.Bloons;
using Il2CppAssets.Scripts.Simulation.Towers;
using Il2CppAssets.Scripts.Simulation.Track;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;

namespace CustomBossChallenge.Config;

/// <summary>
/// What Gildebloon does that the game's boss behaviours can't (#25), run once per simulation frame while it is alive:
/// <list type="bullet">
/// <item><b>Toll</b>: every few seconds it steals a share of the player's cash, up to a cap per tier.</item>
/// <item><b>Vault</b>: what it stole becomes a gold shield (armour) on top of its health.</item>
/// <item><b>Interest</b> (Elite): the Vault grows while nothing hits it.</item>
/// <item><b>Repossession</b>: at each skull it seizes a bigger share of the cash, the towers near it stop attacking
/// for a moment, and the Banana Farms near it stop making money for a while and can't be sold.</item>
/// <item><b>Tax Collectors</b> that leak steal cash instead of lives.</item>
/// <item><b>Payout</b>: when it is beaten the player gets what it stole back, with interest outside CHIMPS.</item>
/// </list>
/// Everything is timed in simulation frames, so fast forward plays out the same. Taking cash with the simulation's
/// own RemoveCash doesn't count as cash spent for Least Cash.
/// </summary>
public static class GildebloonBrain
{
    public const int FramesPerSecond = 60;

    /// <summary>Seconds between thefts</summary>
    public const int TollSeconds = 6;

    /// <summary>
    /// The share of the cash each theft takes: a share of what the player has, with no cap, so sitting on cash costs
    /// (2026-11-21: the old caps, $150 a theft at tier 1, took 0.15 % of $100,000). It all comes back, a fifth more, as
    /// the Payout when it is beaten.
    /// </summary>
    public const double TollShare = 0.02;

    public const double EliteTollShare = 0.04;

    /// <summary>
    /// The Vault's shield is at most this share of the boss's health: with no cap on what it takes, a shield of what it
    /// took could make it unbeatable for a player with a lot of cash
    /// </summary>
    public const double ShieldCapShare = 0.5;

    /// <summary>
    /// The most shield one theft adds, as a share of the boss's health: a Toll, a Tax Collector's leak or Interest; a
    /// skull's Repossession <see cref="MaxSkullShield"/>. Without it a player with a lot of cash had the Vault filled to
    /// its cap at every Toll, faster than most towers could break it, and the boss stood still (2026-11-22: tier 2 with
    /// $847,000, 23,700 shield every 6 s). So the Vault grows over a few thefts, and towers can wear it down.
    /// </summary>
    public const double MaxTheftShield = 0.04;

    public const double MaxSkullShield = 0.12;

    /// <summary>
    /// Shield per stolen dollar, per tier 1-5. The boss's health grows faster from tier to tier than a player's cash
    /// (tier 1 has 12,000 health at about round 40 with some $20,000; tier 5 has 1.9 million at about round 120 with some
    /// $650,000), so with one dollar a point the Vault was 40 % of its health at tier 1 and 1 % at tier 5 (2026-11-22).
    /// These keep a theft of the same share of the cash about the same share of its health at every tier.
    /// </summary>
    internal static readonly double[] ShieldPerCash = [1, 1.4, 2.4, 2.7, 4.9];

    internal static readonly double[] EliteShieldPerCash = [1.5, 3.3, 8.2, 17, 34];

    /// <summary>Shield per stolen dollar for a tier; tiers past 5 are made from tier 5 and get its</summary>
    public static double ShieldPerCashFor(bool elite, int tier)
    {
        var table = elite ? EliteShieldPerCash : ShieldPerCash;
        return table[Math.Clamp(tier, 1, table.Length) - 1];
    }

    /// <summary>Elite's Vault grows by this share each second it isn't hit for <see cref="InterestQuietSeconds"/></summary>
    public const double InterestShare = 0.01;

    public const int InterestQuietSeconds = 2;

    /// <summary>The Payout outside CHIMPS; in CHIMPS the player only gets the stolen cash back</summary>
    public const double PayoutFactor = 1.2;

    /// <summary>A Tax Collector that leaks takes a share of the player's cash, the bigger the bloon the more
    /// (<see cref="Gildebloon.CollectorKinds"/>); this if it is none of those</summary>
    public const double CollectorShare = 0.05;

    /// <summary>How far from Gildebloon towers are seized at a skull, per tier 1-5</summary>
    internal static readonly float[] SeizeRadii = [50, 55, 60, 65, 70];

    /// <summary>How far from Gildebloon towers are seized at a skull</summary>
    public static float SeizeRadiusFor(int tier) => SeizeRadii[Math.Clamp(tier, 1, SeizeRadii.Length) - 1];

    /// <summary>How long Banana Farms near it are seized at a skull</summary>
    public const int SeizeSeconds = 12;

    /// <summary>How long the other towers near it stop attacking at a skull</summary>
    public const int FreezeSeconds = 3;

    /// <summary>At a skull it takes this share of the cash, with no cap</summary>
    public const double SkullShare = 0.1;

    public const double EliteSkullShare = 0.15;

    public const string VaultArmour = "GildeVault";

    public const string FarmBaseId = "BananaFarm";

    /// <summary>The single player's cash manager; there is no player 0 in single player (docs/gildebloon.md)</summary>
    private const int CashIndex = -1;

    private sealed class Vault
    {
        public string BossId = "";
        public bool Elite;
        public int Tier;
        public double Stolen;
        public long NextToll;
        public long LastHit;
        public long NextInterest;
        public float Health;
        public float Armour;
        public int Skulls;
        public readonly Queue<(long Frame, float Damage)> Hits = new();
        public long NextDash;
    }

    /// <summary>How many times Gildebloon dashed this match, for tests</summary>
    public static int Dashes { get; private set; }

    private static Vault? vault;

    /// <summary>The simulation frame last run</summary>
    private static long lastFrame;

    private static readonly List<(Tower Tower, long Until, bool Farm)> Seized = new();

    /// <summary>What the Gildebloon alive has stolen and not paid back yet, or 0</summary>
    public static double Stolen => vault?.Stolen ?? 0;

    /// <summary>Whether a Gildebloon is alive and being run</summary>
    public static bool Active => vault != null;

    /// <summary>What the last Gildebloon paid back when it was beaten, for the HUD and tests</summary>
    public static double LastPayout { get; private set; }

    /// <summary>What Gildebloon and its Tax Collectors have stolen this match, for tests</summary>
    public static double TotalStolen { get; private set; }

    /// <summary>How many Banana Farms are seized now</summary>
    public static int SeizedCount => Seized.Count(entry => entry.Farm);

    /// <summary>How many other towers can't attack now, after a skull</summary>
    public static int FrozenCount => Seized.Count(entry => !entry.Farm);

    /// <summary>
    /// A Repossession at a skull: the cash it seized, how many towers it froze for <see cref="FreezeSeconds"/> and
    /// how many Banana Farms it locked for <see cref="SeizeSeconds"/>, how far it reached, and the boss
    /// </summary>
    public sealed record Repossession(double Cash, int Frozen, int Farms, float Radius, Bloon Boss);

    /// <summary>A Gildebloon reached a skull; the HUD and the map show it</summary>
    public static event Action<Repossession>? Repossessed;

    /// <summary>The towers seized now, the seconds they have left, and whether each is a Banana Farm</summary>
    public static List<(Tower Tower, float SecondsLeft, bool Farm)> SeizedNow() =>
        Seized.Select(entry => (entry.Tower, Math.Max(0, (entry.Until - lastFrame) / (float) FramesPerSecond), entry.Farm)).ToList();

    /// <summary>A Gildebloon took cash, or paid it back (a negative amount); the HUD shows it</summary>
    public static event Action<double>? CashTaken;

    /// <summary>Elite's Interest went into the Vault, with how much</summary>
    public static event Action<double>? InterestAdded;

    /// <summary>Forgets the last match; a new or restarted match starts here</summary>
    public static void Reset()
    {
        foreach (var (tower, _, _) in Seized) Release(tower);
        Seized.Clear();
        vault = null;
        LastPayout = 0;
        TotalStolen = 0;
        Dashes = 0;
    }

    /// <summary>Runs one simulation frame</summary>
    public static void Tick(BossBloonManager manager)
    {
        if (!Experimental.Gildebloon || !CustomBossState.IsActive) return;
        var sim = manager.sim;
        if (sim == null) return;
        var frame = (long) sim.time.elapsed;
        lastFrame = frame;

        ReleaseSeized(frame);

        var boss = manager.CurrentBoss;
        var id = boss?.bloonModel?.id;
        if (boss == null || !Gildebloon.IsGildebloon(id))
        {
            if (vault != null) Gone(sim, manager);
            return;
        }

        if (vault == null || vault.BossId != id) vault = Start(boss, frame);
        var current = vault;

        // Anything that took health or shield counts as a hit
        var armour = Armour(boss);
        if (boss.health < current.Health || armour < current.Armour) current.LastHit = frame;
        if (boss.health < current.Health) current.Hits.Enqueue((frame, current.Health - boss.health));
        current.Health = boss.health;
        current.Armour = armour;
        Skittish(boss, current, frame);

        if (frame >= current.NextToll)
        {
            current.NextToll = frame + TollSeconds * FramesPerSecond;
            Steal(sim, boss, current, Cash(sim) * (current.Elite ? EliteTollShare : TollShare), MaxTheftShield);
        }

        if (current.Elite && frame >= current.NextInterest)
        {
            current.NextInterest = frame + FramesPerSecond;
            if (frame - current.LastHit >= InterestQuietSeconds * FramesPerSecond && current.Stolen > 0)
            {
                var interest = Math.Floor(current.Stolen * InterestShare);
                current.Stolen += interest;
                AddShield(boss, current, interest, MaxTheftShield);
                if (interest > 0) Tell(InterestAdded, interest);
            }
        }

        var skulls = SkullsPassed(boss);
        if (skulls > current.Skulls)
        {
            current.Skulls = skulls;
            var before = current.Stolen;
            Steal(sim, boss, current, Cash(sim) * (current.Elite ? EliteSkullShare : SkullShare), MaxSkullShield);
            var radius = SeizeRadiusFor(current.Tier);
            var (frozen, farms) = Repossess(sim, boss, frame, radius);
            Tell(Repossessed, new Repossession(current.Stolen - before, frozen, farms, radius, boss));
        }
    }

    /// <summary>
    /// Skittish: losing <see cref="Gildebloon.SkittishShare"/> of its health within <see cref="Gildebloon.SkittishSeconds"/>
    /// makes it dash <see cref="Gildebloon.DashDistance"/> ahead along the track, not again for a few seconds
    /// </summary>
    private static void Skittish(Bloon boss, Vault current, long frame)
    {
        var window = (long) (Gildebloon.SkittishSeconds * FramesPerSecond);
        while (current.Hits.Count > 0 && frame - current.Hits.Peek().Frame > window) current.Hits.Dequeue();
        if (frame < current.NextDash || boss.health <= 0) return;
        if (current.Hits.Sum(hit => hit.Damage) < boss.bloonModel.maxHealth * Gildebloon.SkittishShare) return;

        current.Hits.Clear();
        current.NextDash = frame + (long) (Gildebloon.DashCooldownSeconds * FramesPerSecond);
        boss.SetPath(boss.path, boss.DistanceTraveled + Gildebloon.DashDistance, true);
        Dashes++;
    }

    private static Vault Start(Bloon boss, long frame)
    {
        var id = boss.bloonModel.id;
        var elite = id.Contains(TierBossMath.EliteSuffix);
        var tier = TierMath.TierOf(id);
        var factor = ShieldPerCashFor(elite, tier);
        // A boss brought back by a retry or a continued game keeps its shield; the Vault is worked out from it
        var armour = Armour(boss);
        ModHelper.Msg<CustomBossChallenge>($"{id} arrives{(armour > 0 ? $" with {armour} shield" : "")}");
        return new Vault
        {
            BossId = id, Elite = elite, Tier = tier, Stolen = Math.Floor(armour / factor),
            NextToll = frame + TollSeconds * FramesPerSecond, NextInterest = frame + FramesPerSecond, LastHit = frame,
            Health = boss.health, Armour = armour, Skulls = SkullsPassed(boss)
        };
    }

    /// <summary>
    /// The Gildebloon being run is gone: beaten, it pays back what it stole; leaked, the game is over anyway
    /// </summary>
    private static void Gone(Simulation sim, BossBloonManager manager)
    {
        var gone = vault!;
        vault = null;
        foreach (var (tower, _, _) in Seized) Release(tower);
        Seized.Clear();
        if (CustomBossState.HighestDefeatedTier < gone.Tier || gone.Stolen <= 0) return;

        var chimps = InGameData.CurrentGame?.selectedMode == BossOptions.ChimpsMode;
        var payout = Math.Floor(gone.Stolen * (chimps ? 1 : PayoutFactor));
        sim.AddCash(payout, Simulation.CashType.Normal, CashIndex, Simulation.CashSource.Normal, null, false);
        LastPayout = payout;
        Tell(CashTaken, -payout);
        ModHelper.Msg<CustomBossChallenge>($"{gone.BossId} beaten: paid back ${payout} of ${gone.Stolen} stolen");
    }

    private static double Cash(Simulation sim)
    {
        try
        {
            return sim.GetCash(CashIndex);
        }
        catch (Exception)
        {
            return 0;
        }
    }

    private static void Steal(Simulation sim, Bloon boss, Vault current, double amount, double maxShield)
    {
        amount = Math.Floor(Math.Min(amount, Cash(sim)));
        if (amount <= 0) return;
        sim.RemoveCash(amount, Simulation.CashType.Normal, CashIndex, Simulation.CashSource.Normal);
        current.Stolen += amount;
        TotalStolen += amount;
        AddShield(boss, current, amount, maxShield);
        Tell(CashTaken, amount);
    }

    /// <summary>
    /// Tells the screen something happened. What shows it must never stop the simulation (2026-11-22: a cast that
    /// failed in the HUD broke off every Toll), so a failure is logged once and left
    /// </summary>
    private static void Tell<T>(Action<T>? listeners, T what)
    {
        if (listeners == null) return;
        foreach (var listener in listeners.GetInvocationList())
        {
            try
            {
                ((Action<T>) listener)(what);
            }
            catch (Exception e)
            {
                if (toldFailure) continue;
                toldFailure = true;
                ModHelper.Warning<CustomBossChallenge>($"Showing Gildebloon's {typeof(T).Name} failed: {e}");
            }
        }
    }

    private static bool toldFailure;

    /// <summary>A Tax Collector leaked: it takes a share of the cash, into the Vault if Gildebloon is still alive</summary>
    public static void CollectorLeaked(Bloon collector)
    {
        if (!Experimental.Gildebloon || !CustomBossState.IsActive) return;
        var sim = collector.Sim;
        if (sim == null) return;

        var share = Gildebloon.CollectorKind(collector.bloonModel.id)?.Share ?? CollectorShare;
        var amount = Math.Floor(Cash(sim) * share);
        if (amount <= 0) return;

        sim.RemoveCash(amount, Simulation.CashType.Normal, CashIndex, Simulation.CashSource.Normal);
        TotalStolen += amount;
        Tell(CashTaken, amount);
        var boss = InGame.instance?.bridge?.Simulation?.map?.spawner?.bossBloonManager?.CurrentBoss;
        if (vault != null && boss != null)
        {
            vault.Stolen += amount;
            AddShield(boss, vault, amount, MaxTheftShield);
        }
        ModHelper.Msg<CustomBossChallenge>($"A Tax Collector leaked and took ${amount}");
    }

    /// <summary>What is left of the Vault, the boss's armour</summary>
    internal static float Armour(Bloon boss) => boss.HasArmour ? boss.CurrentArmourAmount : 0;

    /// <summary>The most shield the Vault can hold on a boss: <see cref="ShieldCapShare"/> of its health</summary>
    public static int ShieldCap(Bloon boss) => (int) Math.Ceiling(boss.bloonModel.maxHealth * ShieldCapShare);

    private static void AddShield(Bloon boss, Vault current, double cash, double maxShare)
    {
        var amount = (int) Math.Round(cash * ShieldPerCashFor(current.Elite, current.Tier));
        amount = Math.Min(amount, (int) Math.Ceiling(boss.bloonModel.maxHealth * maxShare));
        amount = Math.Min(amount, ShieldCap(boss) - (int) Math.Ceiling(Armour(boss)));
        if (amount <= 0) return;

        BloonArmour? layer = null;
        if (boss.armour != null)
        {
            foreach (var existing in boss.armour)
            {
                if (existing.id == VaultArmour) layer = existing;
            }
        }

        if (layer != null)
        {
            layer.amount += amount;
            layer.original = Math.Max(layer.original, layer.amount);
        }
        else
        {
            boss.AddArmourLayer(new BloonArmour {id = VaultArmour, amount = amount, original = amount});
        }

        current.Armour = Armour(boss);
    }

    /// <summary>How many of its skulls the boss has passed, from its health</summary>
    private static int SkullsPassed(Bloon boss)
    {
        var skulls = SkullModifier.GetSkulls(boss.bloonModel);
        if (skulls == null || boss.bloonModel.maxHealth <= 0) return 0;
        var share = boss.health / (double) boss.bloonModel.maxHealth;
        return skulls.Count(skull => share <= skull);
    }

    /// <summary>Seizes the towers near the boss</summary>
    /// <returns>How many towers it froze, and how many Banana Farms it locked</returns>
    private static (int Frozen, int Farms) Repossess(Simulation sim, Bloon boss, long frame, float radius)
    {
        var position = boss.Position;
        var farms = 0;
        var others = 0;
        foreach (var tower in BTD_Mod_Helper.Extensions.Il2CppGenericIEnumerableExt.ToList(sim.towerManager.GetTowers()))
        {
            if (tower == null || tower.IsDestroyed || Seized.Any(entry => entry.Tower.Pointer == tower.Pointer)) continue;
            var dx = tower.Position.X - position.X;
            var dy = tower.Position.Y - position.Y;
            if (dx * dx + dy * dy > radius * radius) continue;

            var farm = tower.towerModel?.baseId == FarmBaseId;
            tower.SetAttackingDisabled(true);
            if (farm) tower.SetSellingBlocked(true);
            Seized.Add((tower, frame + (farm ? SeizeSeconds : FreezeSeconds) * FramesPerSecond, farm));
            if (farm) farms++;
            else others++;
        }

        ModHelper.Msg<CustomBossChallenge>($"{boss.bloonModel.id} reached a skull: {farms} Banana Farms seized, {others} towers frozen within {radius}");
        return (others, farms);
    }

    private static void ReleaseSeized(long frame)
    {
        for (var i = Seized.Count - 1; i >= 0; i--)
        {
            if (frame < Seized[i].Until) continue;
            Release(Seized[i].Tower);
            Seized.RemoveAt(i);
        }
    }

    private static void Release(Tower tower)
    {
        try
        {
            if (tower.IsDestroyed) return;
            tower.SetAttackingDisabled(false);
            tower.SetSellingBlocked(false);
        }
        catch (Exception)
        {
            // The tower went with the match
        }
    }
}
