using System;
using System.Linq;
using BTD_Mod_Helper;
using Il2CppAssets.Scripts.Data;
using Il2CppAssets.Scripts.Data.Boss;
using Il2CppAssets.Scripts.Unity.Audio;
using Il2CppAssets.Scripts.Unity.Map;
using Il2CppAssets.Scripts.Unity.UI_New;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CustomBossChallenge.Visuals;

/// <summary>
/// The map's boss effects in Advanced Mode (#85). The game puts them on the map once, for the boss it starts as (the
/// first tier's), and keeps them through a restart; here they follow the boss shown whenever it changes. The boss music
/// needs nothing: the game builds the boss track from the boss type when a boss arrives, and
/// <see cref="Patches.MixedBosses"/> has switched the type by then.
/// <br/>
/// Only what the game put on the map is swapped, so a map or an accessibility setting without the effects stays
/// without them.
/// </summary>
public static class BossPresentation
{
    private const string AmbientSuffix = "AmbientMapFx(Clone)";
    private const string TrackSuffix = "TrackFx(Clone)";

    /// <summary>The boss whose map effects were put on the map with this id, so they are swapped only once</summary>
    private static (int Map, BossType Boss)? shown;

    /// <summary>The boss music playing now, or null</summary>
    public static string? MusicPlaying
    {
        get
        {
            var music = Object.FindObjectOfType<AudioFactory>()?.musicFactory;
            return music is {BossMusicIsPlaying: true} ? music.bossMusicClip?.name : null;
        }
    }

    /// <summary>
    /// Puts the map's ambient and track effects of <paramref name="boss"/> in place of the ones there now, if they
    /// differ
    /// </summary>
    public static void ShowMapEffects(BossType boss)
    {
        try
        {
            var loader = UI.instance?.mapLoader;
            var map = MapRoot();
            var data = GameData.Instance?.bosses?.GetBossData(boss);
            if (loader == null || map == null || data == null) return;

            // The game keeps the map, and these effects, through a restart, and builds a new one for a new game. It
            // adds them a moment after the map loads, so a swap is only remembered once there were effects to swap.
            var id = map.GetInstanceID();
            if (shown == (id, boss)) return;
            var effects = Effects(map);
            if (effects.Length == 0) return;
            shown = (id, boss);

            var ambient = effects.Where(effect => effect.name.EndsWith(AmbientSuffix)).ToList();
            if (ambient.Count > 0 && ambient.Any(effect => effect.name != boss + AmbientSuffix) && data.ambientMapFXPrefab is { } fx)
            {
                foreach (var old in ambient) Object.Destroy(old);
                loader.ClearBossAmbientFx();
                loader.AddBossAmbientFx(fx);
            }

            var tracks = effects.Where(effect => effect.name.EndsWith(TrackSuffix)).ToList();
            if (tracks.Count > 0 && tracks.Any(effect => effect.name != boss + TrackSuffix) && data.trackFXPrefab is { } track)
            {
                var position = tracks[0].transform.position;
                foreach (var old in tracks) Object.Destroy(old);
                loader.AddAsset(track, position);
            }
        }
        catch (Exception e)
        {
            ModHelper.Warning<CustomBossChallenge>($"Couldn't show {boss}'s map effects: {e.Message}");
        }
    }

    /// <summary>
    /// How often <see cref="Patches.MixedBosses.KeepPresentation"/> looks at the map, in seconds of real time
    /// </summary>
    public const float CheckInterval = 0.5f;

    /// <summary>The names of the boss effects on the map now, for the tests and the log</summary>
    public static string[] MapEffects => MapRoot() is { } map
        ? Effects(map).Select(effect => effect.name).OrderBy(name => name).ToArray()
        : Array.Empty<string>();

    private static Transform? MapRoot() =>
        UI.instance?.mapLoader?.bossAmbientFx?.transform.parent ?? GameObject.Find("Map")?.transform;

    /// <summary>The boss effects the map has: its children named like the game's ambient and track effects</summary>
    private static GameObject[] Effects(Transform map)
    {
        var found = new System.Collections.Generic.List<GameObject>();
        for (var i = 0; i < map.childCount; i++)
        {
            var child = map.GetChild(i).gameObject;
            if (child.name.EndsWith(AmbientSuffix) || child.name.EndsWith(TrackSuffix)) found.Add(child);
        }
        return found.ToArray();
    }
}
