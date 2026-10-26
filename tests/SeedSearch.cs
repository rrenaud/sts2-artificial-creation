using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;
using Card = ArtificialCreation.Cards.ArtificialCreation;

internal static class SeedSearch
{
    public static void Run(string[] args)
    {
        // Suppress only option text/tooltips, which need Godot's localization assets.
        // Neow's option generation, eligibility rules, RNG and CardFactory stay native.
        new Harmony("ArtificialCreation.SeedSearch.UI").Patch(AccessTools.Method(typeof(EventOption), nameof(EventOption.FromRelic)),
            prefix: new HarmonyMethod(typeof(SeedSearch), nameof(OptionWithoutUi)));
        var requested = args.SkipWhile(a => a != "--seed").Skip(1).FirstOrDefault();
        int found = 0;
        for (int i = 1; i <= (requested == null ? 10000 : 1); i++)
        {
            string seed = SeedHelper.CanonicalizeSeed(requested ?? $"GLASS{i}");
            var character = ModelDb.Character<Defect>();
            var player = (Player)Activator.CreateInstance(typeof(Player), BindingFlags.Instance | BindingFlags.NonPublic,
                null, [character, 1UL, character.StartingHp, character.StartingHp, character.MaxEnergy,
                    character.StartingGold, 3, character.BaseOrbSlotCount, new RelicGrabBag(), UnlockState.all,
                    null, null, null, null, null], null)!;
            AccessTools.Method(typeof(Player), "PopulateStartingDeck").Invoke(player, null);
            foreach (var relic in character.StartingRelics)
                player.AddRelicInternal(relic.ToMutable());
            var run = RunState.CreateForTest([player], ascensionLevel: 10, seed: seed);
            var neow = (Neow)ModelDb.Event<Neow>().ToMutable();
            AccessTools.Property(typeof(EventModel), nameof(EventModel.Owner)).SetValue(neow, player);
            AccessTools.Property(typeof(EventModel), nameof(EventModel.Rng)).SetValue(neow,
                new Rng(run.Rng.Seed + StringHelper.GetDeterministicHashCode(neow.Id.Entry)));
            var options = (IReadOnlyList<EventOption>)AccessTools.Method(typeof(Neow), "GenerateInitialOptions").Invoke(neow, null)!;
            if (!options.Any(o => o.Relic is ArcaneScroll))
            {
                if (requested != null) Console.WriteLine($"{seed}: Arcane Scroll not offered");
                continue;
            }
            var creation = new CardCreationOptions([player.Character.CardPool], CardCreationSource.Other,
                CardRarityOddsType.Uniform, c => c.Rarity == CardRarity.Rare).WithFlags(CardCreationFlags.NoUpgradeRoll);
            var reward = CardFactory.CreateForReward(player, 1, creation).Single().Card;
            if (reward is Card || requested != null)
                Console.WriteLine($"{seed}: Neow=[{string.Join(", ", options.Select(o => o.Relic!.Id.Entry))}]; Arcane Scroll -> {reward.Id}");
            if (reward is Card && ++found >= 3) break;
        }
        if (found == 0) throw new Exception("No matching seed found.");
    }

    public static bool OptionWithoutUi(RelicModel relic, ref EventOption __result)
    {
        __result = ((EventOption)RuntimeHelpers.GetUninitializedObject(typeof(EventOption))).WithRelic(relic);
        return false;
    }
}
