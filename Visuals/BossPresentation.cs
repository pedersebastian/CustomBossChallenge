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
/// without them. While a boss Random picked is a secret, the effects are only switched off, so they can be swapped
/// and switched on again when it arrives (#283), and the mod's own mystery effects take their place (#285,
/// <see cref="MysteryMapFx"/>).
/// </summary>
public static class BossPresentation
{
    internal const string AmbientSuffix = "AmbientMapFx(Clone)";
    internal const string TrackSuffix = "TrackFx(Clone)";

    /// <summary>
    /// The boss whose map effects were put on the map with this id, so they are swapped only once; no boss when they
    /// were switched off
    /// </summary>
    private static (int Map, BossType? Boss)? shown;

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
    /// differ, and switches them on
    /// </summary>
    public static void ShowMapEffects(BossType boss)
    {
        MysteryMapFx.Hide();
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

            foreach (var effect in Effects(map)) effect.SetActive(true);
        }
        catch (Exception e)
        {
            ModHelper.Warning<CustomBossChallenge>($"Couldn't show {boss}'s map effects: {e.Message}");
        }
    }

    /// <summary>
    /// Switches off the boss effects on the map, so it shows no boss's (#283), and shows the mystery effects in their
    /// place (#285). Effects the game adds later are switched off on the next call, so this is called again every
    /// <see cref="CheckInterval"/>.
    /// </summary>
    public static void HideMapEffects()
    {
        try
        {
            var map = MapRoot();
            if (map == null) return;
            var effects = Effects(map);
            if (effects.Length == 0) return;
            shown = (map.GetInstanceID(), null);
            foreach (var effect in effects)
            {
                if (effect.activeSelf) effect.SetActive(false);
            }
            MysteryMapFx.Show(map, effects);
        }
        catch (Exception e)
        {
            ModHelper.Warning<CustomBossChallenge>($"Couldn't hide the boss map effects: {e.Message}");
        }
    }

    /// <summary>
    /// How often <see cref="Patches.MixedBosses.KeepPresentation"/> looks at the map, in seconds of real time
    /// </summary>
    public const float CheckInterval = 0.5f;

    /// <summary>The names of the boss effects the map shows now (the ones switched on), for the tests and the log</summary>
    public static string[] MapEffects => MapRoot() is { } map
        ? Effects(map).Where(effect => effect.activeSelf).Select(effect => effect.name).OrderBy(name => name).ToArray()
        : Array.Empty<string>();

    /// <summary>How many boss effects the map has, shown or switched off, for the tests</summary>
    public static int MapEffectCount => MapRoot() is { } map ? Effects(map).Length : 0;

    /// <summary>Where the game's track effect is, shown or switched off, for the tests</summary>
    public static Vector3? TrackEffectPosition => MapRoot() is { } map &&
                                                  Effects(map).FirstOrDefault(effect => effect.name.EndsWith(TrackSuffix)) is { } track
        ? track.transform.position
        : null;

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
