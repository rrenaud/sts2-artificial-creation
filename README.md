# Artificial Creation

Adds the [Artificial Creation concept by u/Suspicious_Badger_18](https://www.reddit.com/r/slaythespire/comments/1xfbatf/artificial_creation_defect_rare_glass_power/) to Slay the Spire 2's Defect card pool.

- **Download:** [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3827563424) or the [v0.1.0 release zip](https://github.com/rrenaud/sts2-artificial-creation/releases/tag/v0.1.0)
- **Playtest review:** [an Act 1 run with every Artificial Creation screenshot](https://rrenaud.github.io/sts2-artificial-creation/playtest/) (use ← → to flip through the screenshots, or open [`#photos`](https://rrenaud.github.io/sts2-artificial-creation/playtest/#photos))

- **Rare Power**, self-targeted, **3 energy**, upgraded to **2 energy**.
- “Whenever you have an empty Orb Slot, Channel 1 Glass.”
- Fills existing empty slots when played and refills slots immediately after an orb is removed or new slots are gained.
- Channeling into a full queue still replaces the oldest orb with the incoming orb.
- Repeated evokes without removing an orb do not generate Glass. Shatter replaces the original orbs without looping indefinitely.
- Duplicate copies do not multiply the effect; each empty slot needs only one Glass.
- Uses Creative AI's existing card artwork and Spinner's power icon. Original Reddit artwork is not bundled.

## Install

Built against **STS2 v0.111.0**, **.NET 9**, and **BaseLib 3.4.7**.

Extract `dist/ArtificialCreation-0.1.0.zip` into the game's `mods` directory. The result should be:

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
and BaseLib in the game's Mods menu and restart. The card appears in the Defect's
normal rare pool, including rewards and shops. It does not change the starting deck.

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
Capturing and encoding full-resolution PNGs may cause a brief hitch at these events.
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
power application, damage and hook dispatch. It checks cost upgrades, initial filling,
duplicate powers, player isolation, full-queue channeling, removing/non-removing evokes,
slot gain, Shatter's evoke sequence, zero slots, the slot cap, failed-channel cleanup,
and combat end. It uses the game's TestMode and substitutes save-manager access so
it does not need a Godot window or read/write player saves. It does not simulate a
complete run or validate rendered artwork.

Validation on the installed v0.111.0: the Release build completed without warnings,
all 18 integration assertions passed, and the real game's headless startup loaded
Artificial Creation and BaseLib successfully. Live multiplayer and a complete
manual playthrough remain untested.

## Implementation

BaseLib registers the card and supplies its English localization (also used as the
fallback for other languages). Gameplay uses the game's `PowerCmd` and `OrbCmd`.
Two Harmony patches wrap `OrbCmd.Channel` and `OrbCmd.AddSlots`: the first reserves
the incoming orb's place until its asynchronous channel finishes; the second supplies
the slot-gain hook missing in v0.111.0. A per-player guard prevents recursive refills
and is released even when a channel fails. No turn polling or background tasks are used.

See `THIRD_PARTY_NOTICES.md` for attribution and the bundled BaseLib license.
