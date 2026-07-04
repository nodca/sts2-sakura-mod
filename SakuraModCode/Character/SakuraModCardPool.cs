using BaseLib.Abstracts;
using SakuraMod.SakuraModCode.Extensions;
using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Unlocks;

namespace SakuraMod.SakuraModCode.Character;

public class SakuraModCardPool : CustomCardPoolModel
{
    public override string Title => SakuraMod.CharacterId; //This is not a display name.

    public override string BigEnergyIconPath => "charui/big_energy.png".ImagePath();
    public override string TextEnergyIconPath => "charui/text_energy.png".ImagePath();


    // Fallback frame tint for Sakura cards that do not provide a category-specific material.
    public override Color ShaderColor => new("72D8EF");

    //Color of small card icons
    public override Color DeckEntryCardColor => new("ffffff");

    public override bool IsColorless => false;

    protected override CardModel[] GenerateAllCards() =>
        SakuraCardCatalog.AllCardTemplates();

    protected override IEnumerable<CardModel> FilterThroughEpochs(UnlockState unlockState, IEnumerable<CardModel> cards) =>
        base.FilterThroughEpochs(unlockState, cards)
            .Where(card => !SakuraCardCatalog.IsEventOnlyCard(card))
            .Where(card => !SakuraCardCatalog.IsRetiredCard(card));
}
