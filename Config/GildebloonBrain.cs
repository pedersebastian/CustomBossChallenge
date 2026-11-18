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
/// <item><b>Repossession</b>: at each skull the Banana Farms near it stop making money for a while and can't be sold.</item>
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

    public const double TollShare = 0.02;
    public const double EliteTollShare = 0.03;

    /// <summary>The most one theft takes, per tier 1-5; Elite takes twice as much</summary>
    internal static readonly int[] TollCap = [150, 400, 1000, 2500, 6000];

    /// <summary>Shield per stolen dollar</summary>
    public const double ShieldPerCash = 1;

    public const double EliteShieldPerCash = 1.5;

    /// <summary>Elite's Vault grows by this share each second it isn't hit for <see cref="InterestQuietSeconds"/></summary>
    public const double InterestShare = 0.01;

    public const int InterestQuietSeconds = 2;

    /// <summary>The Payout outside CHIMPS; in CHIMPS the player only gets the stolen cash back</summary>
    public const double PayoutFactor = 1.5;

    /// <summary>A Tax Collector that leaks takes this share of the player's cash, up to twice the Toll's cap</summary>
    public const double CollectorShare = 0.05;

    /// <summary>How far from Gildebloon Banana Farms are seized, and for how long</summary>
    public const float SeizeRadius = 80;

    public const int SeizeSeconds = 12;

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
    }

    private static Vault? vault;

    private static readonly List<(Tower Tower, long Until)> Seized = new();

    /// <summary>What the Gildebloon alive has stolen and not paid back yet, or 0</summary>
    public static double Stolen => vault?.Stolen ?? 0;

    /// <summary>Whether a Gildebloon is alive and being run</summary>
    public static bool Active => vault != null;

    /// <summary>What the last Gildebloon paid back when it was beaten, for the HUD and tests</summary>
    public static double LastPayout { get; private set; }

    /// <summary>What Gildebloon and its Tax Collectors have stolen this match, for tests</summary>
    public static double TotalStolen { get; private set; }

    /// <summary>How many Banana Farms are seized now</summary>
    public static int SeizedCount => Seized.Count;

    /// <summary>A Gildebloon took cash, or paid it back (a negative amount); the HUD shows it</summary>
    public static event Action<double>? CashTaken;

    /// <summary>Forgets the last match; a new or restarted match starts here</summary>
    public static void Reset()
    {
        foreach (var (tower, _) in Seized) Release(tower);
        Seized.Clear();
        vault = null;
        LastPayout = 0;
        TotalStolen = 0;
    }

    /// <summary>Runs one simulation frame</summary>
    public static void Tick(BossBloonManager manager)
    {
        if (!Experimental.Gildebloon || !CustomBossState.IsActive) return;
        var sim = manager.sim;
        if (sim == null) return;
        var frame = (long) sim.time.elapsed;

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
        current.Health = boss.health;
        current.Armour = armour;

        if (frame >= current.NextToll)
        {
            current.NextToll = frame + TollSeconds * FramesPerSecond;
            var share = current.Elite ? EliteTollShare : TollShare;
            var cap = TollCap[Math.Clamp(current.Tier, 1, TollCap.Length) - 1] * (current.Elite ? 2 : 1);
            Steal(sim, boss, current, Math.Min(Cash(sim) * share, cap));
        }

        if (current.Elite && frame >= current.NextInterest)
        {
            current.NextInterest = frame + FramesPerSecond;
            if (frame - current.LastHit >= InterestQuietSeconds * FramesPerSecond && current.Stolen > 0)
            {
                var interest = Math.Floor(current.Stolen * InterestShare);
                current.Stolen += interest;
                AddShield(boss, current, interest);
            }
        }

        var skulls = SkullsPassed(boss);
        if (skulls > current.Skulls)
        {
            current.Skulls = skulls;
            Repossess(sim, boss, frame);
        }
    }

    private static Vault Start(Bloon boss, long frame)
    {
        var id = boss.bloonModel.id;
        var elite = id.Contains(TierBossMath.EliteSuffix);
        var factor = elite ? EliteShieldPerCash : ShieldPerCash;
        // A boss brought back by a retry or a continued game keeps its shield; the Vault is worked out from it
        var armour = Armour(boss);
        ModHelper.Msg<CustomBossChallenge>($"{id} arrives{(armour > 0 ? $" with {armour} shield" : "")}");
        return new Vault
        {
            BossId = id, Elite = elite, Tier = TierMath.TierOf(id), Stolen = Math.Floor(armour / factor),
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
        foreach (var (tower, _) in Seized) Release(tower);
        Seized.Clear();
        if (CustomBossState.HighestDefeatedTier < gone.Tier || gone.Stolen <= 0) return;

        var chimps = InGameData.CurrentGame?.selectedMode == BossOptions.ChimpsMode;
        var payout = Math.Floor(gone.Stolen * (chimps ? 1 : PayoutFactor));
        sim.AddCash(payout, Simulation.CashType.Normal, CashIndex, Simulation.CashSource.Normal, null, false);
        LastPayout = payout;
        CashTaken?.Invoke(-payout);
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

    private static void Steal(Simulation sim, Bloon boss, Vault current, double amount)
    {
        amount = Math.Floor(Math.Min(amount, Cash(sim)));
        if (amount <= 0) return;
        sim.RemoveCash(amount, Simulation.CashType.Normal, CashIndex, Simulation.CashSource.Normal);
        current.Stolen += amount;
        TotalStolen += amount;
        AddShield(boss, current, amount);
        CashTaken?.Invoke(amount);
    }

    /// <summary>A Tax Collector leaked: it takes a share of the cash, into the Vault if Gildebloon is still alive</summary>
    public static void CollectorLeaked(Bloon collector)
    {
        if (!Experimental.Gildebloon || !CustomBossState.IsActive) return;
        var sim = collector.Sim;
        if (sim == null) return;

        var elite = collector.bloonModel.id == Gildebloon.EliteCollectorId;
        var tier = vault?.Tier ?? TierMath.VanillaTiers;
        var cap = TollCap[Math.Clamp(tier, 1, TollCap.Length) - 1] * 2 * (elite ? 2 : 1);
        var amount = Math.Floor(Math.Min(Cash(sim) * CollectorShare, cap));
        if (amount <= 0) return;

        sim.RemoveCash(amount, Simulation.CashType.Normal, CashIndex, Simulation.CashSource.Normal);
        TotalStolen += amount;
        CashTaken?.Invoke(amount);
        var boss = InGame.instance?.bridge?.Simulation?.map?.spawner?.bossBloonManager?.CurrentBoss;
        if (vault != null && boss != null)
        {
            vault.Stolen += amount;
            AddShield(boss, vault, amount);
        }
        ModHelper.Msg<CustomBossChallenge>($"A Tax Collector leaked and took ${amount}");
    }

    private static float Armour(Bloon boss) => boss.HasArmour ? boss.CurrentArmourAmount : 0;

    private static void AddShield(Bloon boss, Vault current, double cash)
    {
        var amount = (int) Math.Round(cash * (current.Elite ? EliteShieldPerCash : ShieldPerCash));
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

    private static void Repossess(Simulation sim, Bloon boss, long frame)
    {
        var position = boss.Position;
        var count = 0;
        var farms = BTD_Mod_Helper.Extensions.Il2CppGenericIEnumerableExt.ToList(sim.towerManager.GetTowersByBaseId(FarmBaseId));
        foreach (var tower in farms)
        {
            if (tower == null || Seized.Any(entry => entry.Tower.Pointer == tower.Pointer)) continue;
            var dx = tower.Position.X - position.X;
            var dy = tower.Position.Y - position.Y;
            if (dx * dx + dy * dy > SeizeRadius * SeizeRadius) continue;

            tower.SetAttackingDisabled(true);
            tower.SetSellingBlocked(true);
            Seized.Add((tower, frame + SeizeSeconds * FramesPerSecond));
            count++;
        }

        ModHelper.Msg<CustomBossChallenge>($"{boss.bloonModel.id} reached a skull: {count} Banana Farms seized");
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
