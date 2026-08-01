using Godot;
using MegaCrit.Sts2.Core.Debug;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Afflictions;
using MegaCrit.Sts2.Core.Nodes.Cards;
using STS2RitsuLib.Patching;
using System.Reflection;

namespace SakuraMod.SakuraModCode.Cards;

internal static class SakuraAfflictionVisualLayout
{
    private static readonly Vector2 NativeSize = SakuraCardGeometry.VanillaLayoutSize;
    private const string NativeSceneContractVersion = "0.107.1";
    private static readonly FieldInfo CardOverlayField =
        PrivateAccess.DeclaredField(typeof(NCard), "_cardOverlay");
    private static readonly HashSet<NativeAfflictionVisual> WarnedProfiles = [];
    private const SakuraControlProperty BoxProperties =
        SakuraControlProperty.Anchors
        | SakuraControlProperty.Position
        | SakuraControlProperty.Size
        | SakuraControlProperty.CustomMinimumSize
        | SakuraControlProperty.Scale
        | SakuraControlProperty.PivotOffset;

    public static void Apply(NCard card, SakuraCardMutationLedger ledger)
    {
        var layout = SakuraCardVisualFamilies.Layout(card);
        if (!SakuraCardGeometry.TryProfile(layout, out var geometry)
            || !TryVisual(card.Model?.Affliction, out var visual)
            || CardOverlayField.GetValue(card) is not Control overlay)
        {
            return;
        }

        if (!TryResolve(overlay, visual, out var nodes))
        {
            WarnOnce(visual, layout);
            return;
        }

        var targetSize = geometry.RootSize;
        BorrowCommon(ledger, nodes);
        ApplyCommon(ledger, nodes, targetSize);

        switch (visual)
        {
            case NativeAfflictionVisual.Bound:
                ApplyBound(ledger, nodes, targetSize);
                break;
            case NativeAfflictionVisual.Entangled:
                ApplyEntangled(ledger, nodes, targetSize);
                break;
            case NativeAfflictionVisual.Galvanized:
                ApplyGalvanized(ledger, nodes, targetSize);
                break;
            case NativeAfflictionVisual.Hexed:
                ApplyHexed(ledger, nodes, targetSize);
                break;
            case NativeAfflictionVisual.Ringing:
                ApplyRinging(ledger, nodes, targetSize);
                break;
            case NativeAfflictionVisual.Smog:
                ApplySmog(ledger, nodes, targetSize);
                break;
            case NativeAfflictionVisual.Tainted:
                ApplyTainted(ledger, nodes, targetSize);
                break;
        }
    }

    private static void BorrowCommon(
        SakuraCardMutationLedger ledger,
        ResolvedAfflictionNodes nodes)
    {
        ledger.BorrowPositionBaseline(nodes.Overlay);
        ledger.Borrow(nodes.EffectRoot, BoxProperties);
        ledger.Borrow(nodes.Mask, BoxProperties | SakuraControlProperty.Anchors);
        ledger.Borrow(nodes.Vignette, BoxProperties);
        ledger.Borrow(nodes.Glow, BoxProperties | SakuraControlProperty.Anchors);
    }

    private static void ApplyCommon(
        SakuraCardMutationLedger ledger,
        ResolvedAfflictionNodes nodes,
        Vector2 targetSize)
    {
        var nativeOverlayPosition = ledger.TryGetPositionBaseline(nodes.Overlay, out var baseline)
            ? baseline
            : nodes.Overlay.Position;
        nodes.Overlay.Position = nativeOverlayPosition + targetSize * 0.5f;
        SetBox(nodes.EffectRoot, new Rect2(targetSize * -0.5f, targetSize));
        SakuraCardVisualInfrastructure.ApplyTopLeftAnchors(nodes.Mask);
        SetBox(nodes.Mask, new Rect2(Vector2.Zero, targetSize));
        SetCenteredSquare(nodes.Vignette, targetSize);
        SakuraCardVisualInfrastructure.ApplyTopLeftAnchors(nodes.Glow);
        SetBox(nodes.Glow, new Rect2(Vector2.Zero, targetSize));
    }

    private static void ApplyBound(
        SakuraCardMutationLedger ledger,
        ResolvedAfflictionNodes nodes,
        Vector2 targetSize)
    {
        var border = nodes.Control("card_mask/border");
        var specks = nodes.Node2D("card_mask/vfx_common_specks");
        var container = nodes.Control("vfx_container");
        var main = nodes.Control("vfx_container/main");
        BorrowBoxes(ledger, border, container, main);
        ledger.Borrow(specks, SakuraNode2DProperty.Position);

        SetCenteredSquare(border, targetSize);
        SetCenteredContainer(container, targetSize);
        SetScaledCenteredSquare(
            main,
            targetSize,
            0.5f,
            new Vector2(targetSize.Y * 0.5f, targetSize.Y * 0.5f));
        specks.Position = targetSize * 0.5f;
    }

    private static void ApplyEntangled(
        SakuraCardMutationLedger ledger,
        ResolvedAfflictionNodes nodes,
        Vector2 targetSize)
    {
        var container = nodes.Control("vfx_container");
        var main = nodes.Control("vfx_container/main");
        var leaves = nodes.Control("vfx_container/leaves");
        var particles = nodes.Node2Ds("vfx_container/leaves", 11);
        BorrowBoxes(ledger, container, main);
        ledger.Borrow(leaves, SakuraControlProperty.Position);
        BorrowNodePositions(ledger, particles);

        SetCenteredContainer(container, targetSize);
        SetScaledCenteredSquare(main, targetSize, 0.6f);
        leaves.Position = Vector2.Zero;
        RemapCenteredPositions(particles, EntangledLeafPositions, targetSize);
    }

    private static void ApplyGalvanized(
        SakuraCardMutationLedger ledger,
        ResolvedAfflictionNodes nodes,
        Vector2 targetSize)
    {
        var main = nodes.Control("card_mask/galvanized_main");
        var corners = new[]
        {
            nodes.Node2D("vfx_ui_card_affliction_lightning_corner_upper_left"),
            nodes.Node2D("vfx_ui_card_affliction_lightning_corner_upper_right"),
            nodes.Node2D("vfx_ui_card_affliction_lightning_corner_bottom_right"),
            nodes.Node2D("vfx_ui_card_affliction_lightning_corner_bottom_left"),
        };
        var specks = nodes.Node2D("vfx_common_specks");
        ledger.Borrow(main, BoxProperties);
        BorrowNodePositions(ledger, corners);
        ledger.Borrow(specks, SakuraNode2DProperty.Position);

        SetCenteredSquare(main, targetSize);
        corners[0].Position = Vector2.Zero;
        corners[1].Position = new Vector2(targetSize.X, 0f);
        corners[2].Position = targetSize;
        corners[3].Position = new Vector2(0f, targetSize.Y);
        specks.Position = targetSize * 0.5f;
    }

    private static void ApplyHexed(
        SakuraCardMutationLedger ledger,
        ResolvedAfflictionNodes nodes,
        Vector2 targetSize)
    {
        var main = nodes.Control("card_mask/hexed");
        var top = nodes.Node2D("card_mask/vfx_common_specks_top");
        var bottom = nodes.Node2D("card_mask/vfx_common_specks_bottom");
        ledger.Borrow(main, BoxProperties);
        ledger.Borrow(top, SakuraNode2DProperty.Position);
        ledger.Borrow(bottom, SakuraNode2DProperty.Position);

        SetCenteredSquare(main, targetSize);
        top.Position = new Vector2(targetSize.X * 0.5f, 0f);
        bottom.Position = new Vector2(targetSize.X * 0.5f, targetSize.Y);
    }

    private static void ApplyRinging(
        SakuraCardMutationLedger ledger,
        ResolvedAfflictionNodes nodes,
        Vector2 targetSize)
    {
        var main = nodes.Control("card_mask/ringing_main");
        var specks = nodes.Node2D("card_mask/vfx_common_specks");
        var ornaments = RingingOrnaments
            .Select(profile => (Node: nodes.Control(profile.Path), Profile: profile))
            .ToArray();
        ledger.Borrow(main, BoxProperties);
        ledger.Borrow(specks, SakuraNode2DProperty.Position);
        foreach (var ornament in ornaments)
        {
            ledger.Borrow(
                ornament.Node,
                SakuraControlProperty.Position | SakuraControlProperty.Scale);
        }

        SetCenteredSquare(main, targetSize);
        specks.Position = targetSize * 0.5f;
        var widthRatio = targetSize.X / NativeSize.X;
        var heightRatio = targetSize.Y / NativeSize.Y;
        var positionRatio = new Vector2(widthRatio, heightRatio);
        foreach (var ornament in ornaments)
        {
            var nativePivotPosition = ornament.Profile.Position + ornament.Node.PivotOffset;
            ornament.Node.Position = nativePivotPosition * positionRatio - ornament.Node.PivotOffset;
            ornament.Node.Scale = ornament.Profile.Scale * widthRatio;
        }
    }

    private static void ApplySmog(
        SakuraCardMutationLedger ledger,
        ResolvedAfflictionNodes nodes,
        Vector2 targetSize)
    {
        var main = nodes.Control("card_mask/smog_main");
        var specks = nodes.Node2D("card_mask/vfx_common_specks");
        var outer = nodes.Control("smog_main_outer");
        BorrowBoxes(ledger, main, outer);
        ledger.Borrow(specks, SakuraNode2DProperty.Position);

        SetCenteredSquare(main, targetSize);
        specks.Position = new Vector2(targetSize.X * 0.5f, targetSize.Y);
        SetBox(
            outer,
            new Rect2(
                new Vector2((targetSize.X - targetSize.Y) * 0.5f, -15f * targetSize.Y / NativeSize.Y),
                new Vector2(targetSize.Y, targetSize.Y)),
            new Vector2(targetSize.Y * 0.5f, targetSize.Y * 0.5f));
    }

    private static void ApplyTainted(
        SakuraCardMutationLedger ledger,
        ResolvedAfflictionNodes nodes,
        Vector2 targetSize)
    {
        var container = nodes.Control("vfx_container");
        var main = nodes.Control("vfx_container/main");
        BorrowBoxes(ledger, container, main);

        SetCenteredContainer(container, targetSize);
        SetScaledCenteredSquare(main, targetSize, 0.6f);
    }

    private static bool TryResolve(
        Control overlay,
        NativeAfflictionVisual visual,
        out ResolvedAfflictionNodes nodes)
    {
        nodes = default;
        if (overlay.GetChildCount() != 1
            || overlay.GetChild(0) is not Control effectRoot
            || effectRoot.GetNodeOrNull<TextureRect>("card_mask") is not { } mask
            || effectRoot.GetNodeOrNull<Control>("card_mask/card_vignette") is not { } vignette
            || effectRoot.GetNodeOrNull<Control>("card_mask/card_glow") is not { } glow)
        {
            return false;
        }

        nodes = new ResolvedAfflictionNodes(overlay, effectRoot, mask, vignette, glow);
        return visual switch
        {
            NativeAfflictionVisual.Bound => nodes.HasControls(
                "card_mask/border", "vfx_container", "vfx_container/main")
                && nodes.HasNode2Ds("card_mask/vfx_common_specks"),
            NativeAfflictionVisual.Entangled => nodes.HasControls(
                "vfx_container", "vfx_container/main", "vfx_container/leaves")
                && nodes.HasNode2DChildren("vfx_container/leaves", 11),
            NativeAfflictionVisual.Galvanized => nodes.HasControls("card_mask/galvanized_main")
                && nodes.HasNode2Ds(
                    "vfx_ui_card_affliction_lightning_corner_upper_left",
                    "vfx_ui_card_affliction_lightning_corner_upper_right",
                    "vfx_ui_card_affliction_lightning_corner_bottom_right",
                    "vfx_ui_card_affliction_lightning_corner_bottom_left",
                    "vfx_common_specks"),
            NativeAfflictionVisual.Hexed => nodes.HasControls("card_mask/hexed")
                && nodes.HasNode2Ds(
                    "card_mask/vfx_common_specks_top",
                    "card_mask/vfx_common_specks_bottom"),
            NativeAfflictionVisual.Ringing => nodes.HasControls(
                    "card_mask/ringing_main",
                    "horns_left", "horns_right", "horns_top",
                    "skull_top_left", "skull_top_right", "frame_top")
                && nodes.HasNode2Ds("card_mask/vfx_common_specks"),
            NativeAfflictionVisual.Smog => nodes.HasControls(
                    "card_mask/smog_main", "smog_main_outer")
                && nodes.HasNode2Ds("card_mask/vfx_common_specks"),
            NativeAfflictionVisual.Tainted => nodes.HasControls(
                "vfx_container", "vfx_container/main"),
            _ => false,
        };
    }

    private static bool TryVisual(AfflictionModel? affliction, out NativeAfflictionVisual visual)
    {
        visual = affliction switch
        {
            Bound => NativeAfflictionVisual.Bound,
            Entangled => NativeAfflictionVisual.Entangled,
            Galvanized => NativeAfflictionVisual.Galvanized,
            Hexed => NativeAfflictionVisual.Hexed,
            Ringing => NativeAfflictionVisual.Ringing,
            Smog => NativeAfflictionVisual.Smog,
            Tainted => NativeAfflictionVisual.Tainted,
            _ => default,
        };
        return affliction is Bound or Entangled or Galvanized or Hexed or Ringing or Smog or Tainted;
    }

    private static void BorrowBoxes(SakuraCardMutationLedger ledger, params Control[] controls)
    {
        foreach (var control in controls)
            ledger.Borrow(control, BoxProperties);
    }

    private static void BorrowNodePositions(
        SakuraCardMutationLedger ledger,
        IEnumerable<Node2D> nodes)
    {
        foreach (var node in nodes)
            ledger.Borrow(node, SakuraNode2DProperty.Position);
    }

    private static void RemapCenteredPositions(
        IReadOnlyList<Node2D> nodes,
        IReadOnlyList<Vector2> nativePositions,
        Vector2 targetSize)
    {
        var scale = targetSize / NativeSize;
        for (var index = 0; index < nodes.Count; index++)
            nodes[index].Position = nativePositions[index] * scale;
    }

    private static void SetCenteredContainer(Control control, Vector2 targetSize) =>
        SetBox(control, new Rect2(targetSize * 0.5f, targetSize));

    private static void SetCenteredSquare(Control control, Vector2 targetSize) =>
        SetBox(
            control,
            new Rect2(
                new Vector2((targetSize.X - targetSize.Y) * 0.5f, 0f),
                new Vector2(targetSize.Y, targetSize.Y)));

    private static void SetScaledCenteredSquare(
        Control control,
        Vector2 targetSize,
        float positionScale,
        Vector2? pivotOverride = null) =>
        SetBox(
            control,
            new Rect2(
                new Vector2(targetSize.Y * -positionScale, targetSize.Y * -positionScale),
                new Vector2(targetSize.Y, targetSize.Y)),
            pivotOverride);

    private static void SetBox(Control control, Rect2 box, Vector2? pivotOverride = null)
    {
        var scale = control.Scale;
        var pivot = control.PivotOffset;
        SakuraCardVisualInfrastructure.ApplyTopLeftAnchors(control);
        SakuraCardVisualInfrastructure.ApplyBox(control, box);
        control.Scale = scale;
        control.PivotOffset = pivotOverride ?? pivot;
    }

    private static void WarnOnce(
        NativeAfflictionVisual visual,
        SakuraCardVisualLayout layout)
    {
        if (!WarnedProfiles.Add(visual))
            return;

        var runningVersion = ReleaseInfoManager.Instance.ReleaseInfo?.Version ?? "unknown";
        MainFile.Logger.Warn(
            $"Skipped Sakura {layout} {visual} Affliction visual layout on STS2 {runningVersion}; "
            + $"expected the {NativeSceneContractVersion} scene nodes: {ContractSummary(visual)}.");
    }

    private static string ContractSummary(NativeAfflictionVisual visual) => visual switch
    {
        NativeAfflictionVisual.Bound => "card_mask/border and vfx_container/main",
        NativeAfflictionVisual.Entangled => "vfx_container/main and eleven leaves children",
        NativeAfflictionVisual.Galvanized => "card_mask/galvanized_main and four lightning corners",
        NativeAfflictionVisual.Hexed => "card_mask/hexed and top/bottom specks",
        NativeAfflictionVisual.Ringing => "card_mask/ringing_main, horns, skulls, and frame_top",
        NativeAfflictionVisual.Smog => "card_mask/smog_main and smog_main_outer",
        NativeAfflictionVisual.Tainted => "vfx_container/main",
        _ => "the native profile contract",
    };

    private enum NativeAfflictionVisual
    {
        Bound,
        Entangled,
        Galvanized,
        Hexed,
        Ringing,
        Smog,
        Tainted,
    }

    private readonly record struct RingingOrnament(
        string Path,
        Vector2 Position,
        Vector2 Scale);

    private readonly record struct ResolvedAfflictionNodes(
        Control Overlay,
        Control EffectRoot,
        TextureRect Mask,
        Control Vignette,
        Control Glow)
    {
        public Control Control(string path) => EffectRoot.GetNode<Control>(path);
        public Node2D Node2D(string path) => EffectRoot.GetNode<Node2D>(path);

        public Node2D[] Node2Ds(string parentPath, int count)
        {
            var parent = EffectRoot.GetNode<Control>(parentPath);
            return parent.GetChildren().OfType<Node2D>().Take(count).ToArray();
        }

        public bool HasControls(params string[] paths)
        {
            var root = EffectRoot;
            return paths.All(path => root.GetNodeOrNull<Control>(path) is not null);
        }

        public bool HasNode2Ds(params string[] paths)
        {
            var root = EffectRoot;
            return paths.All(path => root.GetNodeOrNull<Node2D>(path) is not null);
        }

        public bool HasNode2DChildren(string parentPath, int count) =>
            EffectRoot.GetNodeOrNull<Control>(parentPath) is { } parent
            && parent.GetChildren().OfType<Node2D>().Count() == count;
    }

    private static readonly Vector2[] EntangledLeafPositions =
    [
        new(-102f, -218f), new(-82f, -207f), new(105f, -205f),
        new(148f, -140f), new(148f, -33f), new(145f, 81f),
        new(146f, 145f), new(109f, 203f), new(-145f, 110f),
        new(-153f, 27f), new(-158f, -17f),
    ];

    private static readonly RingingOrnament[] RingingOrnaments =
    [
        new("horns_left", new Vector2(-251f, 147f), new Vector2(0.75f, 0.75f)),
        new("horns_right", new Vector2(39f, 147f), new Vector2(0.75f, 0.75f)),
        new("horns_top", new Vector2(-106f, -63f), new Vector2(0.6f, 0.75f)),
        new("skull_top_left", new Vector2(-231f, -107f), new Vector2(0.35f, 0.35f)),
        new("skull_top_right", new Vector2(19f, -107f), new Vector2(0.35f, 0.35f)),
        new("frame_top", new Vector2(-106f, -121f), new Vector2(0.4f, 0.4f)),
    ];
}
