using System.Collections.Generic;
using BTD_Mod_Helper.Api.Components;
using BTD_Mod_Helper.Api.Enums;
using CustomBossChallenge.Config;
using UnityEngine;

namespace CustomBossChallenge.Menus;

/// <summary>
/// The row under the Tier Preview with the setup's difficulty rating (#263): 1 to 10 stars from
/// <see cref="DifficultyRating"/>, lit in gold, the rest dimmed. Its i says the rating is an estimate.
/// </summary>
public sealed class DifficultyStars
{
    public const string RowName = "DifficultyRow";
    public const string StarName = "Star";
    public const float StarSize = 100;
    public const float StarGap = 14;

    public static readonly Color On = new(1f, 0.82f, 0.2f);
    public static readonly Color Off = new(1f, 1f, 1f, 0.25f);

    public GameObject Row { get; }

    /// <summary>The stars, from the first</summary>
    public List<ModHelperImage> Stars { get; } = new();

    /// <summary>How many stars are lit now</summary>
    public int Shown { get; private set; }

    public DifficultyStars(GameObject row)
    {
        Row = row;
        var panel = ModHelperPanel.Create(EditorRows.ControlInfo("Stars", EditorRows.ControlWidth, StarSize));
        panel.transform.SetParent(row.transform, false);
        for (var i = 0; i < DifficultyRating.MaxStars; i++)
        {
            var star = panel.AddImage(new Info(StarName + (i + 1), StarSize / 2 + i * (StarSize + StarGap), 0, StarSize, StarSize)
            {
                Anchor = new Vector2(0, 0.5f), Pivot = new Vector2(0.5f, 0.5f)
            }, VanillaSprites.Star);
            star.Image.raycastTarget = false;
            Stars.Add(star);
        }
    }

    /// <summary>Lights the stars <paramref name="facts"/> earns</summary>
    public void Show(SetupFacts facts)
    {
        Shown = DifficultyRating.Stars(facts);
        for (var i = 0; i < Stars.Count; i++) Stars[i].Image.color = i < Shown ? On : Off;
    }
}
