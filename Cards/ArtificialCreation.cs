using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.ValueProps;

namespace ArtificialCreation.Cards;

[Pool(typeof(DefectCardPool))]
public sealed class ArtificialCreation() : CustomCardModel(2, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    public const int GlassCount = 2;

    public override List<(string, string)> Localization => new CardLoc("Artificial Creation",
        "In multi-enemy fights, this is [gold]Innate[/gold].\nGain {Block:diff()} [gold]Block[/gold].\n" +
        $"[gold]Channel[/gold] {GlassCount} [gold]Glass[/gold].");
    public override string PortraitPath => ModelDb.Card<CreativeAi>().PortraitPath;
    public override string BetaPortraitPath => ModelDb.Card<CreativeAi>().BetaPortraitPath;
    public override Godot.Texture2D CustomPortrait => ModelDb.Card<CreativeAi>().Portrait;

    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(10m, ValueProp.Move)];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromKeyword(CardKeyword.Innate), HoverTipFactory.Static(StaticHoverTip.Channeling),
        HoverTipFactory.FromOrb<GlassOrb>(),
    ];

    // The opening hand pulls Innate cards from the draw pile right after BeforeHandDraw on turn 1.
    // Combat cards are clones of the deck card, so the keyword does not persist past this fight.
    public override Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState)
    {
        if (player == Owner && player.PlayerCombatState?.TurnNumber == 1 && combatState.HittableEnemies.Count > 1
            && !Keywords.Contains(CardKeyword.Innate))
            AddKeyword(CardKeyword.Innate);
        return Task.CompletedTask;
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        for (int i = 0; i < GlassCount; i++)
            await OrbCmd.Channel<GlassOrb>(choiceContext, Owner);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
