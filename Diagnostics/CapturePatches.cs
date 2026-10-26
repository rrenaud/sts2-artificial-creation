using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using Card = ArtificialCreation.Cards.ArtificialCreation;

namespace ArtificialCreation.Diagnostics;

[HarmonyPatch(typeof(NPlayerHand), nameof(NPlayerHand.Add))]
internal static class HandEntryCapture
{
    [HarmonyPostfix]
    static void Postfix(NHandCardHolder __result) => AutoCapture.Safe(() =>
    {
        if (__result.CardModel is not Card card) return;
        var combat = card.Owner.Creature.CombatState;
        AutoCapture.Later($"hand-{__result.GetInstanceId()}", 0.65,
            () => GodotObject.IsInstanceValid(__result) && __result.IsInsideTree() && __result.IsVisibleInTree()
                  && card.Pile?.Type == PileType.Hand && ReferenceEquals(card.Owner.Creature.CombatState, combat),
            () => AutoCapture.Record("hand", "Artificial Creation entered the hand", true, card.Owner));
    });
}

[HarmonyPatch(typeof(NPlayerHand), "StartCardPlay")]
internal static class BeforePlayCapture
{
    [HarmonyPrefix]
    static void Prefix(NHandCardHolder holder) => AutoCapture.Safe(() =>
    {
        if (holder.CardModel is Card card)
            AutoCapture.Record("selected", "Selected Artificial Creation (may be cancelled)", true, card.Owner);
    });
}

[HarmonyPatch(typeof(Hook), nameof(Hook.AfterCardPlayed))]
internal static class PlayedCapture
{
    [HarmonyPrefix]
    static void Prefix(CardPlay cardPlay) => AutoCapture.Safe(() =>
    {
        if (!LocalContext.IsMe(cardPlay.Player)) return;
        string target = cardPlay.Target == null ? "" : $" → {cardPlay.Target.Monster?.Id.Entry ?? "self"}";
        AutoCapture.Record("played", $"Played {cardPlay.Card.Title}{target}" + (cardPlay.IsAutoPlay ? " (auto-play)" : ""), false, cardPlay.Player);
        if (cardPlay.Card is not Card) return;
        // State is final here, but the viewport texture can still be the previous
        // rendered frame. Save state now and take the image after a render tick.
        AutoCapture.Record("resolved", "Artificial Creation resolved", false, cardPlay.Player);
        var combat = cardPlay.Player.Creature.CombatState;
        AutoCapture.Later($"resolved-{RuntimeHelpers.GetHashCode(cardPlay)}", 0.25,
            () => ReferenceEquals(cardPlay.Player.Creature.CombatState, combat),
            () => AutoCapture.Record("after", "After Artificial Creation (settled frame)", true, cardPlay.Player));
    });
}

[HarmonyPatch(typeof(NCardRewardSelectionScreen), nameof(NCardRewardSelectionScreen.RefreshOptions))]
internal static class RewardOffersCapture
{
    internal static readonly ConditionalWeakTable<NCardRewardSelectionScreen, OfferState> Offers = new();
    internal sealed class OfferState { public int Revision; public string[] Cards = []; }
    [HarmonyPostfix]
    static void Postfix(NCardRewardSelectionScreen __instance, IReadOnlyList<CardCreationResult> options) => AutoCapture.Safe(() =>
    {
        var state = Offers.GetOrCreateValue(__instance);
        state.Cards = options.Select(o => o.Card.Title).ToArray();
        int revision = ++state.Revision;
        AutoCapture.Later($"reward-{__instance.GetInstanceId()}-{revision}", 0.7,
            () => GodotObject.IsInstanceValid(__instance) && __instance.IsInsideTree() && __instance.IsVisibleInTree() && state.Revision == revision,
            () => AutoCapture.Record("rewards", "Card rewards: " + string.Join(" | ", state.Cards), true));
    });
}

[HarmonyPatch(typeof(NCardRewardSelectionScreen), "SelectCard")]
internal static class RewardPickCapture
{
    [HarmonyPrefix]
    static void Prefix(NCardRewardSelectionScreen __instance, NCardHolder cardHolder) => AutoCapture.Safe(() =>
    {
        string offered = RewardOffersCapture.Offers.TryGetValue(__instance, out var state) ? string.Join(" | ", state.Cards) : "";
        AutoCapture.Record("pick", $"Picked {cardHolder.CardModel?.Title}. Offered: {offered}", true);
    });
}

[HarmonyPatch(typeof(NCardRewardSelectionScreen), "OnAlternateRewardSelected")]
internal static class RewardSkipCapture
{
    [HarmonyPrefix]
    static void Prefix(int index, IReadOnlyList<CardRewardAlternative> ____extraOptions) => AutoCapture.Safe(() =>
        AutoCapture.Record("skip", "Reward alternative: " + ____extraOptions[index].Title.GetFormattedText(), true));
}

[HarmonyPatch(typeof(NRewardsScreen), nameof(NRewardsScreen.AfterOverlayOpened))]
internal static class FightRewardsCapture
{
    [HarmonyPostfix]
    static void Postfix(NRewardsScreen __instance) => AutoCapture.Later($"fight-rewards-{__instance.GetInstanceId()}", 0.5,
        () => GodotObject.IsInstanceValid(__instance) && __instance.IsInsideTree() && __instance.IsVisibleInTree(),
        () => AutoCapture.Record("fight_rewards", "Fight rewards", true));
}
