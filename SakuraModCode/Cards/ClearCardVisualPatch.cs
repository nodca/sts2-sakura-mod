using Godot;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.addons.mega_text;
using SakuraMod.SakuraModCode.Character;

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>Clear full-card layout adapter: Clear art, scaled text boxes, and the native cost icon.</summary>
internal sealed class ClearCardLayout : ISakuraCardLayoutAdapter
{
    internal const string ArtNodeName = "SakuraClearCardArt";

    private const float SizeScale = 1.05f;

    public static readonly ClearCardLayout Instance = new();

    private static readonly Dictionary<Type, SakuraCardTextureResource> ClearCardArtResources = [];
    private static readonly Rect2 TitleTextBox = Scaled(new Rect2(new Vector2(23f, 8f), new Vector2(160f, 34f)));
    private static readonly Rect2 EnglishNameBox = Scaled(new Rect2(new Vector2(23f, 396f), new Vector2(160f, 30f)));
    private static readonly Rect2 EnergyCostBox = Scaled(new Rect2(new Vector2(-14f, -12f), new Vector2(56f, 56f)));
    private static readonly Rect2 EnergyCostLabelBox = Scaled(new Rect2(new Vector2(12f, -2f), new Vector2(44f, 44f)));
    private static readonly int EnglishNameFontSize = Mathf.RoundToInt(22 * SizeScale);

    private ClearCardLayout()
    {
    }

    public SakuraCardRendererId Id => SakuraCardRendererId.Clear;

    public SakuraCardLayoutGeometry Geometry => SakuraCardGeometry.ClearLayout;

    string ISakuraCardLayoutAdapter.ArtNodeName => ArtNodeName;

    public string EnglishNameNodeName => "SakuraClearCardEnglishName";

    public TextureRect.StretchModeEnum ArtStretchMode => TextureRect.StretchModeEnum.KeepAspectCentered;

    public static void PreloadVisualResources()
    {
        foreach (var cardType in SakuraTransparentCardCatalog.TransparentCardTypes
                     .Concat(SakuraOptionCardCatalog.CardTypes)
                     .Concat(SakuraContentRegistration.ClearLayoutOnlyCardTypes))
            _ = ClearCardTexture(cardType);
        _ = SakuraCardHighlightResources.ResolveClear();
    }

    public bool Accepts(CardModel model) => true;

    public Texture2D? ArtTexture(NCard card, CardModel model) =>
        ClearCardTexture(model.GetType());

    public Texture2D HighlightTexture() =>
        SakuraCardHighlightResources.ResolveClear();

    public Rect2 TitleBox(CardModel model) => TitleTextBox;

    public void ApplyEnglishName(Label label, NCard card, CardModel model) =>
        SakuraCardFace.ApplyEnglishName(
            label,
            EnglishNameBox,
            ClearCardVisualAssets.EnglishName(model),
            visible: true,
            EnglishNameFontSize,
            model);

    public void ApplyDescription(
        NCard card,
        CardModel model,
        MegaRichTextLabel? description,
        SakuraCardFaceState state)
    {
        if (description is null)
            return;

        var showIdentity = card.Visibility == ModelVisibility.Visible;
        var region = state.GetOrCreateDescriptionRegion(card);
        SakuraDescriptionRegion.ApplyBackground(
            region.Background,
            model,
            SakuraCardVisualLayout.Clear,
            showIdentity);
        SakuraDescriptionRegion.ApplyText(
            description,
            SakuraDescriptionRegion.TextBox(SakuraCardVisualLayout.Clear),
            model,
            description.Text,
            visible: true);
    }

    public void ApplyCost(SakuraCardFaceNodes nodes, CardModel model) =>
        SakuraCardFace.ApplyCost(
            nodes.EnergyIcon,
            nodes.EnergyLabel,
            EnergyCostBox,
            EnergyCostLabelBox,
            model.EnergyIcon);

    public Panel? CreateDescriptionPanel() => null;

    public void ApplyHolderExtras(Control? handFlash, SakuraCardMutationLedger ledger)
    {
    }

    private static Texture2D? ClearCardTexture(Type cardType)
    {
        if (!ClearCardArtResources.TryGetValue(cardType, out var resource))
        {
            resource = SakuraCardTextureResource.FromPath(ClearCardVisualAssets.ArtPath(cardType));
            ClearCardArtResources[cardType] = resource;
        }

        return resource.TryResolve(out var texture) ? texture : null;
    }

    private static Rect2 Scaled(Rect2 value) =>
        new(value.Position * SizeScale, value.Size * SizeScale);
}
