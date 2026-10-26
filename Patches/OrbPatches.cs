using System.Runtime.CompilerServices;
using ArtificialCreation.Powers;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace ArtificialCreation.Patches;

// Channel evokes before enqueueing when the queue is full. Filling that temporary
// vacancy from AfterOrbEvoked would make the incoming orb throw "OrbQueue is full".
// Track the entire asynchronous operation per player, including nested channels.
[HarmonyPatch(typeof(OrbCmd), nameof(OrbCmd.Channel),
    [typeof(PlayerChoiceContext), typeof(OrbModel), typeof(Player)])]
internal static class ChannelPatch
{
    private sealed class ChannelState { public int Depth; }
    private static readonly ConditionalWeakTable<Player, ChannelState> States = new();

    internal static bool IsChanneling(Player player) => States.TryGetValue(player, out var state) && state.Depth > 0;

    [HarmonyPrefix]
    private static void Prefix(Player player) => States.GetOrCreateValue(player).Depth++;

    [HarmonyPostfix]
    private static void Postfix(PlayerChoiceContext choiceContext, Player player, ref Task __result) =>
        __result = Complete(__result, player, choiceContext);

    private static async Task Complete(Task original, Player player, PlayerChoiceContext context)
    {
        try { await original; }
        finally { States.GetOrCreateValue(player).Depth--; }
        await ArtificialCreationPower.Refill(player, context);
    }
}

// There is no native AfterOrbSlotsAdded hook in v0.111.0. Wrap the command's
// returned Task so callers await the Glass channels as part of gaining slots.
[HarmonyPatch(typeof(OrbCmd), nameof(OrbCmd.AddSlots))]
internal static class AddSlotsPatch
{
    [HarmonyPostfix]
    private static void Postfix(Player player, ref Task __result) => __result = Complete(__result, player);

    private static async Task Complete(Task original, Player player)
    {
        await original;
        await ArtificialCreationPower.Refill(player, new ThrowingPlayerChoiceContext());
    }
}
