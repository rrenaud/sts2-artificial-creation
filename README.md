# Artificial Creation

Adds two Glass cards to Slay the Spire 2's Defect card pool: Artificial Creation, based on the
[concept by u/Suspicious_Badger_18](https://www.reddit.com/r/slaythespire/comments/1xfbatf/artificial_creation_defect_rare_glass_power/), and Branch Prediction.

- **Download:** [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3827563424) or the [v0.2.0 release zip](https://github.com/rrenaud/sts2-artificial-creation/releases/tag/v0.2.0)
- **Playtest reviews:** [run 2, Acts 1–2 with the Skill version](https://rrenaud.github.io/sts2-artificial-creation/playtest/run2/) and [run 1, Act 1 with the original Power version](https://rrenaud.github.io/sts2-artificial-creation/playtest/) (use ← → to flip through the screenshots)

**Artificial Creation**: Rare Skill, **2 energy**, upgraded to **1 energy**.
> In multi-enemy fights, this is Innate. Gain 10 Block. Channel 2 Glass.

- Becomes Innate for that fight only, when the fight starts with more than one enemy.
- Channeling into a full queue evokes the oldest orbs, as normal.
- Uses Creative AI's existing card artwork. Original Reddit artwork is not bundled.

**Branch Prediction**: Uncommon Skill, **Innate**, **1 energy**, upgraded to **0 energy**.
> Gain 5 Block. If single enemy: Channel 1 Dark. Else: Channel 1 Glass.

- Counts enemies when played, so it Channels Dark once a multi-enemy fight is down to one enemy.
- Uses Hologram's existing card artwork.

Version 0.1.0's Artificial Creation was a 3-cost Power that refilled every empty orb slot with Glass.

## Install

Built against **STS2 v0.111.0**, **.NET 9**, and **BaseLib 3.4.7**.

Extract `dist/ArtificialCreation-0.2.0.zip` into the game's `mods` directory. The result should be:

```text
mods/
  ArtificialCreation/
    ArtificialCreation.dll
    ArtificialCreation.json
  BaseLib/
    BaseLib.dll
    BaseLib.json
    BaseLib.pck
```

If you already have a compatible BaseLib installed (including through Steam Workshop),
keep that installation and copy only the ArtificialCreation folder. Enable the mod
and BaseLib in the game's Mods menu and restart. The cards appear in the Defect's
normal rare and uncommon pools, including rewards and shops. They do not change the starting deck.

All multiplayer participants need the same gameplay mod. Local player-isolation
checks are covered by tests; live network play has not been verified.

## Seed for an immediate copy

With Artificial Creation enabled, start a single-player Defect run using seed
**`GLASS276`** and choose **Arcane Scroll**, Neow's first option. Its random rare
reward is Artificial Creation. The other options are Golden Pearl and Dowsing Rod.
Verified against v0.111.0's native Neow generation and CardFactory, with BaseLib's
model IDs, all Defect unlocks, the normal starting inventory, and Ascension 10.
Other card-pool mods or future game updates can change this result.

Reproduce the check or search for more matching seeds:

```bash
dotnet run --project tests -c Release -- --seed-search --seed GLASS276
dotnet run --project tests -c Release -- --seed-search
```

The search invokes the game's actual Neow option generator and Arcane Scroll's
card-generation settings. Only event-option presentation is stubbed; it does not
launch a run or modify saves.

## Automatic playtest screenshots

Recording is **on by default**. Restart the game after installing this update;
you can continue a saved run without abandoning it. Captures begin from that point.

The recorder saves full-resolution game-window screenshots when Artificial Creation
enters the hand, when you select it to play, and after it resolves. Selection is
labelled separately because dragging a card can be cancelled. It also captures
fight rewards, card offers, your pick, and skip/alternative reward choices. Every
completed card play is logged with its floor, turn and target to show your actual
play sequence. No screenshots are taken for ordinary card plays.

Reports are saved under:

```text
~/.local/share/SlayTheSpire2/artificial_creation_playtests/<timestamp>/
  report.html       # Open locally in a browser; refresh during a run
  events.jsonl      # Structured play/capture log
  0001_hand.png     # Full-resolution screenshots
  ...
```

`latest.txt` in the parent directory points to the newest session. A fresh session
is created when a run is started or loaded. The report groups entries by floor,
lists the hand from left to right, and overlays green boxes showing Artificial
Creation's position. Click a screenshot to see the original image. Delayed shots
wait for the pause overlay to close and are discarded if their screen/card is gone;
reward picks are captured immediately before the choice screen changes.

Use the game's developer console to control recording:

```text
acrecord status
acrecord off
acrecord on
```

The on/off setting persists across restarts. Files stay local; nothing is uploaded.
Frames are grabbed on the game thread; PNG encoding and report writing run in the background.
An I/O/capture error disables recording for the session and logs the error rather
than interrupting gameplay; `acrecord on` retries it.

## Build and test

```bash
dotnet build -c Release
dotnet run --project tests -c Release
python3 scripts/package.py
```

The default assembly path is the standard Linux Steam installation. Override it for
another installation (use the data directory containing `sts2.dll`):

```bash
dotnet build -c Release -p:Sts2DataDir="/path/to/game/data_sts2_platform_arch"
dotnet run --project tests -c Release -p:Sts2DataDir="/path/to/game/data_sts2_platform_arch"
```

The integration harness runs the installed game's real orb queue, orb commands,
Block and hook dispatch. It checks both cards' costs and upgrades, Block, which orbs
they Channel against one or several enemies, when Artificial Creation becomes Innate,
player isolation and full-queue channeling. It uses the game's TestMode and substitutes save-manager access so
it does not need a Godot window or read/write player saves. It does not simulate a
complete run or validate rendered artwork.

Validation on the installed v0.111.0: the Release build completed without warnings,
all 14 integration assertions passed, and the game loaded both cards at startup.
Artificial Creation's Skill version was played through Acts 1–2 (see the run 2 review).
Branch Prediction has not been played in a live run yet, and live multiplayer is untested.

## Implementation

BaseLib registers the cards and supplies their English localization (also used as the
fallback for other languages). Gameplay uses only the game's `CreatureCmd` and `OrbCmd`;
Artificial Creation adds Innate in `BeforeHandDraw` on turn 1, just before the opening
hand pulls Innate cards. Gameplay code uses no Harmony patches; the patches under
`Diagnostics/` only record playtest screenshots.

See `THIRD_PARTY_NOTICES.md` for attribution and the bundled BaseLib license.
