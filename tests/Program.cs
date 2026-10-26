using System.Reflection;
using ArtificialCreation;
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
using Branch = ArtificialCreation.Cards.BranchPrediction;

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
    .Concat([typeof(Card), typeof(Branch)]).ToArray();
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
Check(card.Type == CardType.Skill && card.Rarity == CardRarity.Rare && card.Pool is MegaCrit.Sts2.Core.Models.CardPools.DefectCardPool,
    "Rare Defect skill registered in the Defect pool");
Check(card.EnergyCost.GetWithModifiers(CostModifiers.Local) == 2 && card.DynamicVars.Block.BaseValue == 10m,
    "Base energy cost is 2 with 10 Block");
var upgraded = combat.CreateCard<Card>(player);
upgraded.UpgradeInternal();
Check(upgraded.EnergyCost.GetWithModifiers(CostModifiers.Local) == 1 && upgraded.DynamicVars.Block.BaseValue == 10m,
    "Upgraded energy cost is 1 with the same Block");

await card.BeforeHandDraw(player, context, combat);
Check(!card.Keywords.Contains(CardKeyword.Innate), "Not Innate in a single-enemy fight");

var branch = combat.CreateCard<Branch>(player);
Check(branch.Type == CardType.Skill && branch.Pool is MegaCrit.Sts2.Core.Models.CardPools.DefectCardPool
      && branch.Keywords.Contains(CardKeyword.Innate) && branch.EnergyCost.GetWithModifiers(CostModifiers.Local) == 1
      && branch.DynamicVars.Block.BaseValue == 5m, "Branch Prediction is an Innate 1-cost Defect skill with 5 Block");
var upgradedBranch = combat.CreateCard<Branch>(player);
upgradedBranch.UpgradeInternal();
Check(upgradedBranch.EnergyCost.GetWithModifiers(CostModifiers.Local) == 0, "Upgraded Branch Prediction costs 0");
await (Task)AccessTools.Method(typeof(Branch), "OnPlay").Invoke(branch, [context, null])!;
Check(player.Creature.Block == 5 && queue.Orbs.Count == 1 && queue.Orbs[0] is DarkOrb,
    "Branch Prediction against one enemy gains 5 Block and channels Dark");
await OrbCmd.EvokeNext(context, player);
player.Creature.LoseBlockInternal(player.Creature.Block);
var second = combat.CreateCreature(ModelDb.Monster<SludgeSpinner>().ToMutable(), CombatSide.Enemy, "test2");
second.SetMaxHpInternal(10000);
second.SetCurrentHpInternal(10000);
combat.AddCreature(second);
await card.BeforeHandDraw(other, context, combat);
Check(!card.Keywords.Contains(CardKeyword.Innate), "Another player's hand draw does not make the card Innate");
await card.BeforeHandDraw(player, context, combat);
Check(card.Keywords.Contains(CardKeyword.Innate), "Becomes Innate before the opening hand in a multi-enemy fight");

await (Task)AccessTools.Method(typeof(Branch), "OnPlay").Invoke(branch, [context, null])!;
Check(queue.Orbs.Count == 1 && queue.Orbs[0] is GlassOrb, "Branch Prediction against several enemies channels Glass");
await OrbCmd.EvokeNext(context, player);
player.Creature.LoseBlockInternal(player.Creature.Block);

await OrbCmd.Channel<LightningOrb>(context, player);
var initial = queue.Orbs[0];
// OnPlayWrapper drives card-pile visuals, so invoke the card's own effect directly.
await (Task)AccessTools.Method(typeof(Card), "OnPlay").Invoke(card, [context, null])!;
Check(player.Creature.Block == 10, "Playing the card gains 10 Block");
Check(queue.Orbs.Count == 3 && ReferenceEquals(queue.Orbs[0], initial) && queue.Orbs.Skip(1).All(o => o is GlassOrb),
    "Playing the card channels exactly 2 Glass after the existing orb");
Check(other.PlayerCombatState!.OrbQueue.Orbs.Count == 0 && other.Creature.Block == 0, "Another player is untouched");
await (Task)AccessTools.Method(typeof(Card), "OnPlay").Invoke(card, [context, null])!;
Check(queue.Orbs.Count == 3 && queue.Orbs.All(o => o is GlassOrb) && !queue.Orbs.Contains(initial),
    "Playing into a full queue evokes the oldest orbs as normal");
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
