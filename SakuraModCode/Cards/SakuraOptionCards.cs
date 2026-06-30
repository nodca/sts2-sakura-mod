using BaseLib.Abstracts;
using Godot;
using SakuraMod.SakuraModCode.Extensions;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using SakuraMod.SakuraModCode.Character;

namespace SakuraMod.SakuraModCode.Cards;

public abstract class SakuraOptionCard(CardType type) :
    CustomCardModel(0, type, CardRarity.Basic, TargetType.Self, showInCardLibrary: false, autoAdd: false)
{
    public override CardPoolModel Pool => ModelDb.CardPool<SakuraModCardPool>();

    public override string CustomPortraitPath => SakuraCardFrameVisuals.BigPortraitPath(this);
    public override string PortraitPath => SakuraCardFrameVisuals.PortraitPath(this);
    public override string BetaPortraitPath => SakuraCardFrameVisuals.PortraitPath(this);
    public override Texture2D? CustomFrame => SakuraCardFrameVisuals.CustomFrameTexture(this);
    public override Material? CreateCustomFrameMaterial => SakuraCardFrameVisuals.PlainFrameMaterial;
    protected override IEnumerable<string> ExtraRunAssetPaths => SakuraCardFrameVisuals.RunAssetPaths(this);

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play) =>
        Task.CompletedTask;
}

public class ChoiceManifestChoice() : SakuraOptionCard(CardType.Skill)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Manifest];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1)];

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1);
}

public class ChoiceDrawChoice() : SakuraOptionCard(CardType.Skill)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1)];

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1);
}

public class TrueOrFalseDrawChoice() : SakuraOptionCard(CardType.Skill)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(2)];

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1);
}

public class TrueOrFalseEnergyChoice() : SakuraOptionCard(CardType.Skill)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new EnergyVar(1)];

    protected override void OnUpgrade() => DynamicVars.Energy.UpgradeValueBy(1);
}

public class SealedBookSealChoice() : SakuraOptionCard(CardType.Skill)
{
}

public class SealedBookReleaseChoice() : SakuraOptionCard(CardType.Skill)
{
}
