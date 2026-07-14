using Godot;
using SakuraMod.SakuraModCode.Extensions;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using SakuraMod.SakuraModCode.Classic.Character;
using SakuraMod.SakuraModCode.Character;
using STS2RitsuLib.Scaffolding.Content;

namespace SakuraMod.SakuraModCode.Cards;

public abstract class SakuraOptionCard(CardType type) :
    ModCardTemplate(0, type, CardRarity.Basic, TargetType.Self, showInCardLibrary: false)
{
    public abstract string EnglishName { get; }

    public override CardPoolModel Pool => ModelDb.CardPool<ClassicSakuraCardPool>();
    public override bool CanBeGeneratedInCombat => false;

    public override string CustomPortraitPath => SakuraCardFrameVisuals.BigPortraitPath(this);
    public override string PortraitPath => SakuraCardFrameVisuals.PortraitPath(this);
    public override string BetaPortraitPath => SakuraCardFrameVisuals.PortraitPath(this);
    public override Material? CustomFrameMaterial => SakuraCardFrameVisuals.PlainFrameMaterial;
    protected override IEnumerable<string> ExtraRunAssetPaths => SakuraCardFrameVisuals.RunAssetPaths(this);

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play) =>
        Task.CompletedTask;
}

internal static class SakuraOptionCardCatalog
{
    public static IReadOnlyList<Type> CardTypes { get; } =
        typeof(SakuraOptionCard).Assembly.GetTypes()
            .Where(static type =>
                !type.IsAbstract
                && typeof(SakuraOptionCard).IsAssignableFrom(type))
            .OrderBy(static type => type.FullName, StringComparer.Ordinal)
            .ToList();
}

public class ChoiceManifestChoice() : SakuraOptionCard(CardType.Skill)
{
    public override string EnglishName => "MANIFEST";
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Manifest];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar("ManifestCards", 1)];

    protected override void OnUpgrade() => DynamicVars["ManifestCards"].UpgradeValueBy(1);
}

public class ChoiceDrawChoice() : SakuraOptionCard(CardType.Skill)
{
    public override string EnglishName => "DRAW";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar("DrawCards", 2)];
}

public class TrueOrFalseDrawChoice() : SakuraOptionCard(CardType.Skill)
{
    public override string EnglishName => "FALSE";
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(2)];
}

public class TrueOrFalseEnergyChoice() : SakuraOptionCard(CardType.Skill)
{
    public override string EnglishName => "TRUE";
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Stabilize];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new EnergyVar(2)];
}
