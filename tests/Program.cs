using System.Reflection;
using ArtificialCreation;
using ArtificialCreation.Powers;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Unlocks;
using Card = ArtificialCreation.Cards.ArtificialCreation;

// Run the installed game's real orb commands and hooks without a Godot window.
// Only save-manager access is stubbed; TestMode disables audiovisual waits.
TestMode.IsOn = true;
new Harmony("ArtificialCreation.Tests").Patch(AccessTools.PropertyGetter(typeof(SaveManager), "Instance"),
    prefix: new HarmonyMethod(typeof(TestPatches), nameof(TestPatches.NoSaveManager)));
ModEntry.Initialize();
if (args.Contains("--seed-search"))
    new Harmony("ArtificialCreation.SeedSearch.Ids").CreateClassProcessor(typeof(BaseLib.Patches.Content.PrefixIdPatch)).Patch();
var types = typeof(ModelDb).Assembly.GetTypes()
    .Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(AbstractModel)) && t.GetConstructor(Type.EmptyTypes) != null)
    .Concat([typeof(Card), typeof(ArtificialCreationPower)]).ToArray();
AccessTools.Field(typeof(ModelDb), "_allAbstractModelSubtypes").SetValue(null, types);
ModelDb.Init(types);
if (args.Contains("--seed-search"))
{
    SeedSearch.Run(args);
    return;
}
var context = new ThrowingPlayerChoiceContext();
int assertions = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    assertions++;
    Console.WriteLine($"PASS: {message}");
}

Player CreatePlayer(ulong id) => (Player)Activator.CreateInstance(typeof(Player), BindingFlags.Instance | BindingFlags.NonPublic,
    null, [ModelDb.Character<Defect>(), id, 75, 75, 3, 0, 3, 3, new RelicGrabBag(), UnlockState.all,
        null, null, null, null, null], null)!;
var player = CreatePlayer(1);
var other = CreatePlayer(2);
var run = RunState.CreateForTest([player, other], acts: [], seed: "ARTIFICIAL");
var combat = new CombatState(runState: run);
combat.AddPlayer(player);
combat.AddPlayer(other);
player.ResetCombatState();
other.ResetCombatState();
var enemy = combat.CreateCreature(ModelDb.Monster<SludgeSpinner>().ToMutable(), CombatSide.Enemy, "test");
enemy.SetMaxHpInternal(10000);
enemy.SetCurrentHpInternal(10000);
combat.AddCreature(enemy);
// Set up a live turn without starting the UI-driven turn loop or touching saves.
var manager = CombatManager.Instance;
var field = AccessTools.Field(manager.GetType(), "_turnState");
var turn = Activator.CreateInstance(field.FieldType, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
    null, [combat], null)!;
AccessTools.Property(turn.GetType(), "IsInProgress").SetValue(turn, true);
field.SetValue(manager, turn);
var queue = player.PlayerCombatState!.OrbQueue;

var card = combat.CreateCard<Card>(player);
Check(card.Type == CardType.Power && card.Rarity == CardRarity.Rare && card.Pool is MegaCrit.Sts2.Core.Models.CardPools.DefectCardPool,
    "Rare Defect power registered in the Defect pool");
Check(card.EnergyCost.GetWithModifiers(CostModifiers.Local) == 3, "Base energy cost is 3");
card.UpgradeInternal();
Check(card.EnergyCost.GetWithModifiers(CostModifiers.Local) == 2, "Upgraded energy cost is 2");

await OrbCmd.Channel<LightningOrb>(context, player);
var initial = queue.Orbs[0];
await PowerCmd.Apply<ArtificialCreationPower>(context, player.Creature, 1, player.Creature, card);
Check(queue.Orbs.Count == 3 && ReferenceEquals(queue.Orbs[0], initial) && queue.Orbs.Skip(1).All(o => o is GlassOrb),
    "Applying the power fills both empty slots and preserves the starting orb");
Check(other.PlayerCombatState!.OrbQueue.Orbs.Count == 0, "Another player's queue is untouched");
var beforeDuplicate = queue.Orbs.ToArray();
await PowerCmd.Apply<ArtificialCreationPower>(context, player.Creature, 1, player.Creature, card);
Check(player.Creature.Powers.OfType<ArtificialCreationPower>().Count() == 1 && queue.Orbs.SequenceEqual(beforeDuplicate),
    "Duplicate applications do not channel extra orbs");

await OrbCmd.Channel<FrostOrb>(context, player);
Check(queue.Orbs.Count == 3 && queue.Orbs.Last() is FrostOrb && !queue.Orbs.Contains(initial),
    "Full-queue channel preserves the incoming orb and evokes exactly the front orb");
var front = queue.Orbs[0];
await OrbCmd.EvokeNext(context, player, dequeue: false);
Check(queue.Orbs.Count == 3 && ReferenceEquals(front, queue.Orbs[0]), "Non-removing evoke leaves the queue unchanged");
await OrbCmd.EvokeNext(context, player);
Check(queue.Orbs.Count == 3 && !queue.Orbs.Contains(front) && queue.Orbs.Last() is GlassOrb,
    "Removing evoke immediately refills one Glass");
var last = queue.Orbs.Last();
await OrbCmd.EvokeLast(context, player);
Check(queue.Orbs.Count == 3 && !queue.Orbs.Contains(last) && queue.Orbs.Last() is GlassOrb,
    "EvokeLast refills the vacated slot");

await OrbCmd.AddSlots(player, 2);
Check(queue.Capacity == 5 && queue.Orbs.Count == 5 && queue.Orbs.TakeLast(2).All(o => o is GlassOrb),
    "Capacitor-style slot gain fills before AddSlots completes");
// Shatter snapshots the initial count, then evokes each front orb twice.
var beforeShatter = queue.Orbs.ToArray();
foreach (var _ in beforeShatter)
{
    await OrbCmd.EvokeNext(context, player, dequeue: false);
    await OrbCmd.EvokeNext(context, player);
}
Check(queue.Orbs.Count == 5 && queue.Orbs.All(o => o is GlassOrb) && !queue.Orbs.Intersect(beforeShatter).Any(),
    "Shatter's double-evoke sequence replaces every original orb and terminates");
await OrbCmd.AddSlots(player, 100);
Check(queue.Capacity == 10 && queue.Orbs.Count == 10, "Slot gain respects the game's ten-slot cap");
OrbCmd.RemoveSlots(player, 10);
await OrbCmd.Channel<GlassOrb>(context, player);
Check(queue.Capacity == 0 && queue.Orbs.Count == 0, "Zero slots cannot create orbs or loop");
await OrbCmd.AddSlots(player, 2);
Check(queue.Orbs.Count == 2, "Regaining slots after reaching zero fills them immediately");
await OrbCmd.AddSlots(other, 2);
Check(other.PlayerCombatState.OrbQueue.Orbs.Count == 0, "Slot-gain patch has no effect without the power");

// Fault injection validates the asynchronous channel guard is released on failure.
var canonical = ModelDb.Orb<GlassOrb>();
try { await OrbCmd.Channel(context, canonical, player); throw new Exception("Expected canonical-orb rejection"); }
catch (MegaCrit.Sts2.Core.Models.Exceptions.CanonicalModelException) { }
await OrbCmd.EvokeNext(context, player);
Check(queue.Orbs.Count == 2, "Channel guard recovers after a failed channel");
AccessTools.Property(turn.GetType(), "IsInProgress").SetValue(turn, false);
var countBeforeEnd = queue.Orbs.Count;
await OrbCmd.AddSlots(player, 2);
Check(queue.Orbs.Count == countBeforeEnd && queue.Capacity == 2, "No channeling or slot changes after combat ends");
Console.WriteLine($"All {assertions} integration assertions passed against the installed sts2.dll.");

// Diagnostic output must survive refreshes and treat game text as text, not HTML.
var reportPath = Path.Combine(Path.GetTempPath(), "ac-report-test-" + Guid.NewGuid());
var report = new ArtificialCreation.Diagnostics.PlaytestReport(reportPath, "GLASS276");
report.Append(new(1, "hand", 1, 2, "Card <script> & screenshot", ["Defend", "Artificial Creation"], "Energy 3",
    "0001_hand.png", [new(25, 70, 12, 25, "Artificial Creation · 2/5")]));
report.Append(new(2, "played", 1, 2, "Played Artificial Creation", [], ""));
string renderedReport = File.ReadAllText(Path.Combine(reportPath, "report.html"));
Check(renderedReport.Contains("&lt;script&gt; &amp;") && !renderedReport.Contains("Card <script>"), "Report escapes game text");
Check(renderedReport.Contains("2. Artificial Creation") && renderedReport.Contains("left:25.000%"), "Report includes hand position and screenshot marker");
Check(File.ReadAllLines(Path.Combine(reportPath, "events.jsonl")).Length == 2 && renderedReport.Contains("Played Artificial Creation"), "Report preserves screenshot and play sequence after updates");
Directory.Delete(reportPath, recursive: true);

static class TestPatches
{
    public static bool NoSaveManager(ref SaveManager __result) { __result = null!; return false; }
}
