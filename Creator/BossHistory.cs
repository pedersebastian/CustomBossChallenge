using System;
using CustomBossChallenge.Config;
using CustomBossChallenge.Menus;
using Il2CppAssets.Scripts.Models.ServerEvents;
using Il2CppAssets.Scripts.Unity.UI_New.ChallengeEditor;

namespace CustomBossChallenge.Creator;

/// <summary>
/// The state from before a change to the boss options. A boss-only step holds what the Boss section shows; a step
/// with <see cref="Setup"/> holds the whole challenge, for Paste, Load, Random and the resets, which change the
/// Challenge Editor's rules too.
/// </summary>
public sealed record BossStep(BossOptions Options, float BossSpeed, float BossHealth, int MaxParagons, string? Name,
    DailyChallengeModel? Setup = null);

/// <summary>
/// Undo and Redo for the Boss Creator (issue #36). Only the boss options are undone, never a change to one of the
/// editor's own rules on its own; a whole setup (Paste, Load, Random, a reset) is one step.
/// </summary>
public static class BossHistory
{
    private static readonly UndoHistory<BossStep> History = new();

    /// <summary>
    /// Set while a step is put back, so putting it back isn't recorded as a change of its own
    /// </summary>
    private static bool restoring;

    public static int UndoCount => History.UndoCount;
    public static int RedoCount => History.RedoCount;

    public static void Clear() => History.Clear();

    /// <summary>
    /// Remembers the boss options before a change to one of them
    /// </summary>
    /// <param name="control">The control that changes, so quick changes to it (dragging a slider) are one step</param>
    public static void RecordChange(ChallengeEditor editor, string? control)
    {
        if (!BossMode.Active || restoring || editor.dcm == null) return;
        History.Record(BossOnly(editor.dcm), control, DateTime.UtcNow);
    }

    /// <summary>
    /// Remembers the whole setup before it is replaced or reset
    /// </summary>
    public static void RecordSetup(ChallengeEditor editor)
    {
        if (!BossMode.Active || restoring || editor.dcm == null) return;
        History.Record(Whole(editor), null, DateTime.UtcNow);
    }

    public static bool Undo(ChallengeEditor editor) => Step(editor, undo: true);

    public static bool Redo(ChallengeEditor editor) => Step(editor, undo: false);

    private static bool Step(ChallengeEditor editor, bool undo)
    {
        if (!BossMode.Active || editor.dcm == null) return false;

        BossStep Current(BossStep step) => step.Setup != null ? Whole(editor) : BossOnly(editor.dcm);
        if (!(undo ? History.TryUndo(Current, out var restore) : History.TryRedo(Current, out restore))) return false;

        restoring = true;
        try
        {
            if (restore.Setup != null)
            {
                BossMode.Replace(editor, restore.Options, restore.Setup);
            }
            else
            {
                PutBack(editor, restore);
            }
        }
        finally
        {
            restoring = false;
        }

        return true;
    }

    private static BossStep BossOnly(DailyChallengeModel dcm) => new(BossMode.Options,
        dcm.bloonModifiers?.bossSpeedMultiplier ?? 1, dcm.bloonModifiers?.healthMultipliers?.boss ?? 1, dcm.maxParagons,
        dcm.name);

    private static BossStep Whole(ChallengeEditor editor)
    {
        // The editor keeps some of its fields in the UI until it writes them into the model
        editor.ApplyUiSettingsToDCM();
        return BossOnly(editor.dcm) with {Setup = editor.dcm.Clone()};
    }

    private static void PutBack(ChallengeEditor editor, BossStep step)
    {
        var dcm = editor.dcm;
        dcm.bloonModifiers ??= new BloonModifiers();
        dcm.bloonModifiers.healthMultipliers ??= new BloonHealthMultipliers();
        dcm.bloonModifiers.bossSpeedMultiplier = step.BossSpeed;
        dcm.bloonModifiers.healthMultipliers.boss = step.BossHealth;
        dcm.maxParagons = step.MaxParagons;

        // The name followed the boss; one the player typed is theirs and stays
        if (step.Name != null && (BossMode.IsDefaultName(dcm.name) || ExampleSetups.Named(dcm.name) != null))
        {
            dcm.name = step.Name;
            if (editor.chalName != null) editor.chalName.text = step.Name;
        }

        BossMode.Options = step.Options;
        BossEditorSection.Current?.Load(dcm, step.Options);
        BossMode.Save();
    }
}
