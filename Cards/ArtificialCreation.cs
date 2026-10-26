using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Orbs;
using ArtificialCreation.Powers;

namespace ArtificialCreation.Cards;

[Pool(typeof(DefectCardPool))]
public sealed class ArtificialCreation() : CustomCardModel(3, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    public const string RulesText = "Whenever you have an empty Orb Slot, [gold]Channel[/gold] 1 [gold]Glass[/gold].";

    public override List<(string, string)> Localization => new CardLoc("Artificial Creation", RulesText);
    public override string PortraitPath => ModelDb.Card<CreativeAi>().PortraitPath;
    public override string BetaPortraitPath => ModelDb.Card<CreativeAi>().BetaPortraitPath;
    public override Godot.Texture2D CustomPortrait => ModelDb.Card<CreativeAi>().Portrait;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [HoverTipFactory.Static(StaticHoverTip.Channeling), HoverTipFactory.FromOrb<GlassOrb>()];

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.TriggerAnim(Owner.Creature, "PowerUp", Owner.Character.PowerUpAnimDelay);
        await PowerCmd.Apply<ArtificialCreationPower>(choiceContext, Owner.Creature, 1, Owner.Creature, this);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
