# Save file fixtures

The mod's own files from every released file version, so `SaveFormatTests` can check that this version still reads
them. The file names are `<file>-v<version>-<release>.json`. A `v99-future` file stands for a file from a newer version
of the mod, which must be left alone.

| Fixture | Where it comes from |
|---|---|
| `continue-v1-2.7.0.json` | A real `Continue.json` written by 2.7–2.9 (version 1). The map saves are short stand-ins, and a second snapshot with too many defeated tiers was added. |
| `continue-v2-2.9.0.json` | Written by hand to 2.9.0's format: a different boss per tier, with no `RandomTiers` or `BossEventRounds` yet |
| `continue-v3-2.10.0.json` | Written by hand to 2.10.0's format: Boss Event Rounds and a random tier |
| `lastsetup-v2-2.0.0.json` | Written by hand to 2.0.0's format: `ExtraTierHealth` and no `SpeedPerTier`/`HealthPerTier` yet |
| `lastsetup-v2-2.10.0.json` | A real `LastSetup.json` written by 2.10.0 |
| `savedsetups-v1-2.5.0.json` | Two setups from the real file below, in 2.5.0's format, which is the same |
| `savedsetups-v1-2.10.0.json` | A real `SavedSetups.json` written by 2.10.0, with four damaged entries added: no name, no code, a name already used, and `null` |

The challenges and the share codes are the mod's own JSON and codes, which these files only carry as text; they are
tested elsewhere (`CodeFormatTests`, the property tests and the in-game tests). The map saves of `Continue.json` need
the game, so they are stand-ins here; `ContinueTests` checks real ones in-game.

**When you raise a file's `Version`:** first add a fixture of the old version to this folder: a file written by the
released mod when one is at hand, or else one written by hand to that release's code (`git show <tag>:Persistence/…`),
as the table above says for each. `SaveFormatTests` fails when a version has no fixture.
