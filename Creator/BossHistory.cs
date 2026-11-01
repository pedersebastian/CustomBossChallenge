using System;
using CustomBossChallenge.Config;
using CustomBossChallenge.Menus;
using CustomBossChallenge.Persistence;
using Il2CppAssets.Scripts.Models.ServerEvents;
using Il2CppAssets.Scripts.Unity.UI_New.ChallengeEditor;
using UnityEngine;

namespace CustomBossChallenge.Creator;

/// <summary>
/// The state from before a change. A boss-only step holds what the Boss section shows. A step with
/// <see cref="Setup"/> holds the whole challenge: for Paste, Load, Random and the resets, or, with
/// <see cref="RulesOnly"/>, for a change to the Challenge Editor's own rules, which puts back only the rules.
/// </summary>
public sealed record BossStep(BossOptions Options, float BossSpeed, float BossHealth, int MaxParagons, string? Name,
    DailyChallengeModel? Setup = null, bool RulesOnly = false);

/// <summary>
/// Undo and Redo for the Boss Creator (issue #36): changes to the boss options, changes to the Challenge Editor's own
/// rules (map, difficulty, cash, lives, towers, modifiers, ...), and a whole setup (Paste, Load, Random, a reset) as
/// one step. The challenge name is the player's to type and isn't undone.
/// </summary>
public static class BossHistory
{
    private static readonly UndoHistory<BossStep> History = new();

    /// <summary>
    /// Set while a step is put back, so putting it back isn't recorded as a change of its own
    /// </summary>
    private static bool restoring;

    /// <summary>
    /// How often the editor's rules are compared with the last ones seen, in seconds
    /// </summary>
    public const float RulesCheckInterval = 0.5f;

    /// <summary>
    /// The rules as last seen, to notice a change, and the challenge with them, to go back to
    /// </summary>
    private static string? lastRules;
    private static DailyChallengeModel? lastRulesSetup;

    /// <summary>
    /// Set after the whole setup changed in one step, so the next look at the rules takes them as they are
    /// </summary>
    private static bool resync = true;

    private static float nextRulesCheck;

    public static int UndoCount => History.UndoCount;
    public static int RedoCount => History.RedoCount;

    public static void Clear()
    {
        History.Clear();
        resync = true;
    }

    /// <summary>
    /// Notices a change to the editor's own rules and remembers the rules from before it as one step. The editor's
    /// fields and panels work in many ways, so instead of following each of them the rules are compared with the last
    /// ones seen. Called every frame while nothing else has the keyboard, so a text field still being typed in or a
    /// panel still open becomes one step once it is done.
    /// </summary>
    /// <param name="now">Look now, not only every <see cref="RulesCheckInterval"/></param>
    public static void WatchRules(ChallengeEditor editor, bool now = false)
    {
        if (!BossMode.Active || restoring || editor.dcm == null) return;
        if (!now && !resync && Time.realtimeSinceStartup < nextRulesCheck) return;
        nextRulesCheck = Time.realtimeSinceStartup + RulesCheckInterval;

        editor.ApplyUiSettingsToDCM();
        var rules = Rules(editor.dcm);
        if (!resync && lastRulesSetup != null && rules != lastRules)
        {
            History.Record(BossOnly(editor.dcm) with {Setup = lastRulesSetup, RulesOnly = true}, null, DateTime.UtcNow);
        }

        resync = false;
        lastRules = rules;
        lastRulesSetup = editor.dcm.Clone();
    }

    /// <summary>
    /// The challenge's rules as text, without what the Boss section and the name field set
    /// </summary>
    private static string Rules(DailyChallengeModel dcm)
    {
        var copy = dcm.Clone();
        copy.name = "";
        copy.maxParagons = 0;
        if (copy.bloonModifiers != null)
        {
            copy.bloonModifiers.bossSpeedMultiplier = 1;
            if (copy.bloonModifiers.healthMultipliers != null) copy.bloonModifiers.healthMultipliers.boss = 1;
        }

        return DcmJson.Serialize(copy);
    }

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

        // A rule changed just before is a step of its own
        WatchRules(editor, now: true);
        History.Record(Whole(editor), null, DateTime.UtcNow);
        resync = true;
    }

    public static bool Undo(ChallengeEditor editor) => Step(editor, undo: true);

    public static bool Redo(ChallengeEditor editor) => Step(editor, undo: false);

    private static bool Step(ChallengeEditor editor, bool undo)
    {
        if (!BossMode.Active || editor.dcm == null) return false;

        // A rule changed since the last look is the step to undo
        WatchRules(editor, now: true);
        BossStep Current(BossStep step) => step.Setup != null ? Whole(editor) with {RulesOnly = step.RulesOnly} : BossOnly(editor.dcm);
        if (!(undo ? History.TryUndo(Current, out var restore) : History.TryRedo(Current, out restore))) return false;

        restoring = true;
        try
        {
            if (restore.RulesOnly)
            {
                // Only the rules go back: the boss options and the name stay as they are now
                var setup = restore.Setup!;
                setup.bloonModifiers ??= new BloonModifiers();
                setup.bloonModifiers.healthMultipliers ??= new BloonHealthMultipliers();
                setup.bloonModifiers.bossSpeedMultiplier = editor.dcm.bloonModifiers?.bossSpeedMultiplier ?? 1;
                setup.bloonModifiers.healthMultipliers.boss = editor.dcm.bloonModifiers?.healthMultipliers?.boss ?? 1;
                setup.maxParagons = editor.dcm.maxParagons;
                setup.name = editor.dcm.name;
                BossMode.Replace(editor, BossMode.Options, setup);
            }
            else if (restore.Setup != null)
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

            // The rules as they are now are the ones the next change is compared with
            if (restore.Setup != null) resync = true;
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
