using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using SakuraMod.SakuraModCode.Character;
using STS2RitsuLib.Scaffolding.Content;

namespace SakuraMod.SakuraModCode.Cards;

public abstract class SakuraOptionCard(CardType type) :
    ModCardTemplate(0, type, CardRarity.Basic, TargetType.Self, showInCardLibrary: false)
{
    public abstract string EnglishName { get; }

    public override CardPoolModel Pool => ModelDb.CardPool<ClassicSakuraCardPool>();
    public override bool CanBeGeneratedInCombat => false;

    public override string CustomPortraitPath => CardModel.MissingPortraitPath;
    public override string PortraitPath => CardModel.MissingPortraitPath;
    public override string BetaPortraitPath => CardModel.MissingPortraitPath;
    public override Material? CustomFrameMaterial => SakuraCardFrameVisuals.PlainFrameMaterial;
    protected override IEnumerable<string> ExtraRunAssetPaths => SakuraCardFrameVisuals.RunAssetPaths(this);
    internal virtual IEnumerable<CardKeyword> ReferencedKeywords => [];
    internal virtual IEnumerable<string> ReferencedStaticHoverTipKeys => [];

    protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay play) =>
        Task.CompletedTask;
}

internal static class SakuraOptionCardCatalog
{
    public static IReadOnlyList<Type> CardTypes { get; } =
    [
        typeof(ChoiceDrawChoice),
        typeof(ChoiceManifestChoice),
        typeof(TrueOrFalseDrawChoice),
        typeof(TrueOrFalseEnergyChoice),
        typeof(ExchangeMemoryChoice),
        typeof(ExchangeExhaustChoice),
        typeof(ExchangeDrawChoice),
        typeof(ExchangeDiscardChoice)
    ];
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
    internal override IEnumerable<string> ReferencedStaticHoverTipKeys =>
        [SakuraCardHoverTips.TemporaryTipKey];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(2)];
}

public class TrueOrFalseEnergyChoice() : SakuraOptionCard(CardType.Skill)
{
    public override string EnglishName => "TRUE";
    public override IEnumerable<CardKeyword> CanonicalKeywords => [SakuraKeywords.Stabilize];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new EnergyVar(2)];
}

internal enum ExchangePileKind
{
    Memory,
    Exhaust,
    Draw,
    Discard
}

public abstract class ExchangePileOptionCard() : SakuraOptionCard(CardType.Skill)
{
    internal abstract ExchangePileKind Kind { get; }
    public override int MaxUpgradeLevel => 0;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(0)];

    internal void SetDisplayedCount(int count) =>
        DynamicVars.Cards.BaseValue = Math.Max(0, count);
}

public class ExchangeMemoryChoice() : ExchangePileOptionCard
{
    public override string EnglishName => "MEMORY";
    internal override ExchangePileKind Kind => ExchangePileKind.Memory;
    internal override IEnumerable<string> ReferencedStaticHoverTipKeys => [SakuraMemoryPile.PileId];
}

public class ExchangeExhaustChoice() : ExchangePileOptionCard
{
    public override string EnglishName => "EXHAUST";
    internal override ExchangePileKind Kind => ExchangePileKind.Exhaust;
}

public class ExchangeDrawChoice() : ExchangePileOptionCard
{
    public override string EnglishName => "DRAW";
    internal override ExchangePileKind Kind => ExchangePileKind.Draw;
}

public class ExchangeDiscardChoice() : ExchangePileOptionCard
{
    public override string EnglishName => "DISCARD";
    internal override ExchangePileKind Kind => ExchangePileKind.Discard;
}
