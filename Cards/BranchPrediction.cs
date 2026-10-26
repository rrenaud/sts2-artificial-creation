using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
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
public sealed class BranchPrediction() : CustomCardModel(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override List<(string, string)> Localization => new CardLoc("Branch Prediction",
        "Gain {Block:diff()} [gold]Block[/gold].\nIf single enemy: [gold]Channel[/gold] 1 [gold]Dark[/gold].\n" +
        "Else: [gold]Channel[/gold] 1 [gold]Glass[/gold].");
    public override string PortraitPath => ModelDb.Card<Hologram>().PortraitPath;
    public override string BetaPortraitPath => ModelDb.Card<Hologram>().BetaPortraitPath;
    public override Godot.Texture2D CustomPortrait => ModelDb.Card<Hologram>().Portrait;

    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(5m, ValueProp.Move)];
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Innate];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.Static(StaticHoverTip.Channeling), HoverTipFactory.FromOrb<DarkOrb>(),
        HoverTipFactory.FromOrb<GlassOrb>(),
    ];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", Owner.Character.CastAnimDelay);
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, cardPlay);
        // Checked on play, so enemies that died or were summoned since the fight began count.
        if (Owner.Creature.CombatState?.HittableEnemies.Count > 1)
            await OrbCmd.Channel<GlassOrb>(choiceContext, Owner);
        else
            await OrbCmd.Channel<DarkOrb>(choiceContext, Owner);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
