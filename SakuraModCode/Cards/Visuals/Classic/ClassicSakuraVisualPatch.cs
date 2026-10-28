using Godot;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.addons.mega_text;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Extensions;

namespace SakuraMod.SakuraModCode.Cards;

internal static class ClassicCardVisualAssets
{
    private static readonly IReadOnlyDictionary<Type, string> SpellArtStems = new Dictionary<Type, string>
    {
        [typeof(SpellSeal)] = "seal_p.png",
        [typeof(SpellRelease)] = "release_p.png",
        [typeof(SpellTurn)] = "turn_p.png",
        [typeof(SpellEmptySpell)] = "empty_spell_p.png",
        [typeof(SpellHuoShen)] = "huoshen_p.png",
        [typeof(SpellLeiDi)] = "leidi_p.png",
        [typeof(SpellFengHua)] = "fenghua_p.png",
        [typeof(SpellShuiLong)] = "shuilong_p.png"
    };
    private static readonly Dictionary<string, SakuraCardTextureResource> TextureResources = [];

    public static IEnumerable<string> RunAssetPaths(SakuraSourceCard card)
    {
        if (!SakuraCardVisualFamilies.UsesClassicLayout(card))
        {
            yield return card.PortraitPath;
            yield break;
        }

        if (IsFullFaceCard(card))
        {
            yield return FullFacePath(card);
            yield return UnknownFullFacePath(card);
            if (EnergyIconPath(card) is { } energyIconPath)
                yield return energyIconPath;
            if (TextEnergyIconPath(card) is { } textEnergyIconPath)
                yield return textEnergyIconPath;
            yield return FlashPath();
            foreach (var path in SakuraDescriptionRegion.AssetPaths(card))
                yield return path;
            yield break;
        }
    }

    public static Texture2D Texture(string path)
    {
        if (!TextureResources.TryGetValue(path, out var resource))
        {
            resource = SakuraCardTextureResource.FromPath(path);
            TextureResources[path] = resource;
        }

        return resource.ResolveRequired("Classic Sakura visual texture");
    }

    public static string FullFacePath(SakuraSourceCard card) =>
        FullFacePath(card, ArtStem(card.GetType()).NormalClassicArtStem());

    public static string UnknownFullFacePath(SakuraSourceCard card) =>
        FullFacePath(card, "unknown.png");

    public static string FullFacePath(SakuraSourceCard card, string fileName) =>
        Path.Join(FullFaceFamilyDirectory(card), fileName).ClassicFullFaceImagePath();

    public static string ArtStem(Type type)
    {
        if (SpellArtStems.TryGetValue(type, out var spellStem))
            return spellStem;

        var metadata = SakuraCardCatalog.MetadataFor(type);
        if (metadata.VisualRoute != SakuraSourceCardVisualRoute.Classic || metadata.Identity is not { } identity)
            throw new InvalidOperationException($"Missing Classic Sakura art mapping for {type.Name}.");

        return $"the_{identity.ToString().ToLowerInvariant()}_p.png";
    }

    public static string FlashPath() =>
        Path.Join("general", "flash", "flash.png").ClassicCardUiImagePath();

    public static string? EnergyIconPath(SakuraSourceCard card) =>
        card.IsClowCard
            ? ClassicCardEnergyIcon.ClowBigPath
            : card.IsSakuraCard
                ? ClassicCardEnergyIcon.SakuraBigPath
                : card is SpellSeal or SpellRelease or SpellTurn
                    ? ClassicCardEnergyIcon.ClowBigPath
                    : null;

    public static string? TextEnergyIconPath(SakuraSourceCard card) =>
        card.IsClowCard
            ? ClassicCardEnergyIcon.ClowTextPath
            : card.IsSakuraCard
                ? ClassicCardEnergyIcon.SakuraTextPath
                : card is SpellSeal or SpellRelease or SpellTurn
                    ? ClassicCardEnergyIcon.ClowTextPath
                    : null;

    internal static string FullFaceFamilyDirectory(SakuraSourceCard card) =>
        card.IsClowCard
            ? "clow"
            : card.IsSakuraCard
                ? "sakura"
                : card.IsSpellCard
                    ? "spell"
                    : throw new InvalidOperationException($"Classic full-card face assets are not defined for {card.GetType().Name}.");

    private static bool IsFullFaceCard(SakuraSourceCard card) =>
        card.IsClassicSourceCard || card.IsSpellCard;

}

internal static class ClassicCardTextLayoutPolicy
{
    private static readonly Rect2 ClowTitleBox = new(new Vector2(28f, 1f), new Vector2(165f, 42f));
    private static readonly Rect2 SakuraTitleBox = new(new Vector2(28f, 6f), new Vector2(165f, 42f));

    // Clow and Sakura full faces carry their printed English title in the
    // artwork, so the runtime label would double-print over it. Cards whose art
    // lacks a printed title (the nameless card) still get the runtime label.
    private static readonly HashSet<string> ArtStemsWithoutPrintedTitle = new()
    {
        "the_love",
    };

    internal static Rect2 TitleBox(SakuraSourceCard model) =>
        model.IsClowCard || model is SpellSeal or SpellRelease or SpellTurn
            ? ClowTitleBox
            : SakuraTitleBox;

    internal static bool ShowsRuntimeEnglishName(SakuraSourceCard model) =>
        !model.IsSpellCard
        && ArtStemsWithoutPrintedTitle.Contains(
            Path.GetFileNameWithoutExtension(
                ClassicCardVisualAssets.ArtStem(model.GetType()).NormalClassicArtStem()));
}


/// <summary>
/// Classic full-face layout adapter for Clow, Sakura, and Spell cards: printed face art,
/// per-form title and English name boxes, and the Classic description theme.
/// </summary>
internal sealed class ClassicCardLayout : ISakuraCardLayoutAdapter
{
    internal const string FaceNodeName = "ClassicSakuraFace";

    public static readonly ClassicCardLayout Instance = new();

    private static readonly Rect2 ClowEnglishNameBox = new(new Vector2(23f, 433f), new Vector2(175f, 34f));
    private static readonly Rect2 SakuraEnglishNameBox = new(new Vector2(23f, 425f), new Vector2(175f, 34f));
    private static readonly Rect2 DescriptionPanelBox = new(new Vector2(16f, 273f), new Vector2(190f, 140f));
    private static readonly Rect2 DescriptionBox = new(new Vector2(16f, 273f), new Vector2(190f, 140f));
    private static readonly Rect2 EnergyCostBox = new(new Vector2(-14f, -12f), new Vector2(56f, 56f));
    private static readonly Rect2 EnergyCostLabelBox = new(new Vector2(12f, -2f), new Vector2(44f, 44f));
    private static readonly Color DescriptionTextColor = new(0.98f, 0.94f, 0.78f, 1f);
    private static readonly Color DescriptionTextOutlineColor = new(0.08f, 0.05f, 0.04f, 0.92f);
    private static readonly Color DescriptionPanelColor = new(0f, 0f, 0f, 0.72f);
    private const int EnglishNameFontSize = 22;
    private const int DescriptionPanelCornerRadius = 3;
    private const int DescriptionMinFontSize = 12;
    private const int DescriptionMaxFontSize = 18;
    private const int DescriptionOutlineSize = 2;

    private ClassicCardLayout()
    {
    }

    public SakuraCardRendererId Id => SakuraCardRendererId.Classic;

    public SakuraCardLayoutGeometry Geometry => SakuraCardGeometry.ClassicLayout;

    public string ArtNodeName => FaceNodeName;

    public string EnglishNameNodeName => "ClassicSakuraEnglishName";

    public TextureRect.StretchModeEnum ArtStretchMode => TextureRect.StretchModeEnum.Scale;

    public bool Accepts(CardModel model) => model is SakuraSourceCard;

    public Texture2D? ArtTexture(NCard card, CardModel model)
    {
        var source = (SakuraSourceCard)model;
        return ClassicCardVisualAssets.Texture(card.Visibility == ModelVisibility.Visible
            ? ClassicCardVisualAssets.FullFacePath(source)
            : ClassicCardVisualAssets.UnknownFullFacePath(source));
    }

    public Texture2D HighlightTexture() =>
        SakuraCardHighlightResources.ResolveClassic();

    public Rect2 TitleBox(CardModel model) =>
        ClassicCardTextLayoutPolicy.TitleBox((SakuraSourceCard)model);

    public void ApplyEnglishName(Label label, NCard card, CardModel model)
    {
        var source = (SakuraSourceCard)model;
        SakuraCardFace.ApplyEnglishName(
            label,
            source.IsSakuraCard ? SakuraEnglishNameBox : ClowEnglishNameBox,
            ClassicEnglishName(source.GetType()),
            card.Visibility == ModelVisibility.Visible && ClassicCardTextLayoutPolicy.ShowsRuntimeEnglishName(source),
            EnglishNameFontSize,
            model);
    }

    public void ApplyDescription(
        NCard card,
        CardModel model,
        MegaRichTextLabel? description,
        SakuraCardFaceState state)
    {
        if (description is null)
            return;

        var showFaceIdentity = card.Visibility == ModelVisibility.Visible;
        if (!SakuraDescriptionRegion.AppliesTo(model))
        {
            if (state.TryGetOrCreateDescriptionPanel(card, out var panel))
                ApplyPanelLayout(panel, showFaceIdentity);
            ApplyPlainDescriptionLayout(description);
        }
        else
        {
            var region = state.GetOrCreateDescriptionRegion(card);
            SakuraDescriptionRegion.ApplyBackground(
                region.Background,
                model,
                SakuraCardVisualLayout.Classic,
                showFaceIdentity);
            SakuraDescriptionRegion.ApplyText(
                description,
                SakuraDescriptionRegion.TextBox(SakuraCardVisualLayout.Classic),
                model,
                description.Text,
                visible: true);
        }

        SakuraCardFace.ApplyTextCanvasColor(description);
        SakuraCardVisualInfrastructure.ApplyThemeColorOverride(description, SakuraCardFace.FontColorName, DescriptionTextColor);
        SakuraCardVisualInfrastructure.ApplyThemeColorOverride(description, SakuraCardFace.DefaultColorName, DescriptionTextColor);
        SakuraCardVisualInfrastructure.ApplyThemeColorOverride(description, SakuraCardFace.FontOutlineColorName, DescriptionTextOutlineColor);
        SakuraCardVisualInfrastructure.ApplyThemeConstantOverride(description, SakuraCardFace.OutlineSizeName, DescriptionOutlineSize);
    }

    public void ApplyCost(SakuraCardFaceNodes nodes, CardModel model)
    {
        var source = (SakuraSourceCard)model;
        if (!source.ShowsEnergyCost)
        {
            SakuraCardFace.Hide(nodes.EnergyIcon);
            SakuraCardFace.Hide(nodes.EnergyLabel);
            return;
        }

        SakuraCardFace.ApplyCost(
            nodes.EnergyIcon,
            nodes.EnergyLabel,
            EnergyCostBox,
            EnergyCostLabelBox,
            ClassicCardVisualAssets.EnergyIconPath(source) is { } iconPath
                ? ClassicCardVisualAssets.Texture(iconPath)
                : null);
    }

    public Panel? CreateDescriptionPanel() =>
        SakuraCardVisualInfrastructure.CreatePanel(
            "ClassicSakuraDescriptionPanel",
            DescriptionPanelColor,
            DescriptionPanelCornerRadius);

    public void ApplyHolderExtras(Control? handFlash, SakuraCardMutationLedger ledger)
    {
        if (handFlash is not TextureRect flash)
            return;

        ledger.BorrowTexture(flash);
        SakuraCardVisualInfrastructure.SetTextureIfDifferent(
            flash,
            ClassicCardVisualAssets.Texture(ClassicCardVisualAssets.FlashPath()));
    }

    private static void ApplyPanelLayout(Panel panel, bool visible)
    {
        SakuraCardVisualInfrastructure.ApplyBox(panel, DescriptionPanelBox);
        if (panel.Visible != visible)
            panel.Visible = visible;
        if (panel.ZIndex != 0)
            panel.ZIndex = 0;
        if (panel.MouseFilter != Control.MouseFilterEnum.Ignore)
            panel.MouseFilter = Control.MouseFilterEnum.Ignore;
    }

    private static void ApplyPlainDescriptionLayout(MegaRichTextLabel description)
    {
        SakuraCardVisualInfrastructure.ApplyBox(description, DescriptionBox);
        if (!description.Visible)
            description.Visible = true;
        if (description.ScrollActive)
            description.ScrollActive = false;
        if (description.FitContent)
            description.FitContent = false;
        if (!description.IsHorizontallyBound)
            description.IsHorizontallyBound = true;
        if (!description.IsVerticallyBound)
            description.IsVerticallyBound = true;
        if (description.MinFontSize != DescriptionMinFontSize)
            description.MinFontSize = DescriptionMinFontSize;
        if (description.MaxFontSize != DescriptionMaxFontSize)
            description.MaxFontSize = DescriptionMaxFontSize;
        if (description.ZIndex != 0)
            description.ZIndex = 0;
    }

    private static string ClassicEnglishName(Type cardType)
    {
        var artStem = ClassicCardVisualAssets.ArtStem(cardType).NormalClassicArtStem();
        var name = Path.GetFileNameWithoutExtension(artStem);
        if (name.StartsWith("the_", StringComparison.Ordinal))
            name = name["the_".Length..];

        return $"THE {name.Replace('_', ' ').ToUpperInvariant()}";
    }
}
