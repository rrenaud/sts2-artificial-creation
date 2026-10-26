using ArtificialCreation.Patches;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Models.Powers;

namespace ArtificialCreation.Powers;

public sealed class ArtificialCreationPower : CustomPowerModel
{
    private bool _refilling;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override string CustomPackedIconPath => ModelDb.Power<SpinnerPower>().PackedIconPath;
    public override string CustomBigIconPath => ModelDb.Power<SpinnerPower>().ResolvedBigIconPath;
    public override List<(string, string)> Localization => new PowerLoc(
        "Artificial Creation", Cards.ArtificialCreation.RulesText, Cards.ArtificialCreation.RulesText);

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [HoverTipFactory.Static(StaticHoverTip.Channeling), HoverTipFactory.FromOrb<GlassOrb>()];

    public override Task AfterApplied(Creature? applier, CardModel? cardSource) =>
        FillEmptySlots(new ThrowingPlayerChoiceContext());

    public override Task AfterOrbEvoked(PlayerChoiceContext choiceContext, OrbModel orb, IEnumerable<Creature> targets) =>
        orb.Owner == Owner.Player ? FillEmptySlots(choiceContext) : Task.CompletedTask;

    public override Task AfterOrbChanneled(PlayerChoiceContext choiceContext, Player player, OrbModel orb) =>
        player == Owner.Player ? FillEmptySlots(choiceContext) : Task.CompletedTask;

    internal async Task FillEmptySlots(PlayerChoiceContext choiceContext)
    {
        var player = Owner.Player;
        if (_refilling || player?.PlayerCombatState is not { } state || ChannelPatch.IsChanneling(player))
            return;

        _refilling = true;
        try
        {
            var queue = state.OrbQueue;
            while (!CombatManager.Instance.IsOverOrEnding && Owner.CombatState != null && Owner.IsAlive
                   && queue.Orbs.Count < queue.Capacity)
            {
                int previousCount = queue.Orbs.Count;
                await OrbCmd.Channel<GlassOrb>(choiceContext, player);
                // A hook from another mod may prevent channeling. Never spin forever.
                if (queue.Orbs.Count <= previousCount)
                    break;
            }
        }
        finally
        {
            _refilling = false;
        }
    }

    internal static Task Refill(Player player, PlayerChoiceContext choiceContext) =>
        player.Creature.GetPower<ArtificialCreationPower>()?.FillEmptySlots(choiceContext) ?? Task.CompletedTask;
}
