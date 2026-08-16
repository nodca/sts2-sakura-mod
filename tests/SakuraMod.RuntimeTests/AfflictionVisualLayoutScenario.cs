using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Afflictions;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Pooling;
using MegaCrit.Sts2.Core.TestSupport;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.TestProtocol;
using System.Reflection;

namespace SakuraMod.RuntimeTests;

internal static class AfflictionVisualLayoutScenario
{
    private const float GeometryTolerance = 0.1f;
    private static readonly FieldInfo CardOverlayField = typeof(NCard).GetField(
        "_cardOverlay",
        BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(typeof(NCard).FullName, "_cardOverlay");

    public static async Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request,
        RuntimeAssertionCollector assertions)
    {
        var context = await CombatScenarioContext.StartAsync(request);
        var combat = await context.EnterWeakSlimesCombatAsync();
        var player = context.Player;
        NCard? vanillaNode = null;
        NCard? classicNode = null;
        NCard? clearNode = null;
        NCard? brokenContractNode = null;

        try
        {
            var vanilla = combat.CreateCard<DefendIronclad>(player);
            var classic = combat.CreateCard<ClowShield>(player);
            var clear = combat.CreateCard<Kindness>(player);
            vanillaNode = CreateAttachedCard(vanilla);
            classicNode = CreateAttachedCard(classic);
            clearNode = CreateAttachedCard(clear);

            var vanillaGeometry = await InspectAllAfflictions(player, vanilla, vanillaNode, "vanilla", assertions);
            var classicGeometry = await InspectAllAfflictions(player, classic, classicNode, "classic", assertions);
            var clearGeometry = await InspectAllAfflictions(player, clear, clearNode, "clear", assertions);
            var brokenContract = combat.CreateCard<ClowShield>(player);
            brokenContractNode = CreateAttachedCard(brokenContract);
            var compatibilityGeometry = await InspectMissingNodeFallback(
                player,
                brokenContract,
                brokenContractNode,
                assertions);

            RuntimeTestHost.WriteCheckpoint(
                request,
                "affliction_visual_layout_probed",
                "All seven native Affliction overlays were measured against live Classic and Clear card geometry.");

            return new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["vanilla"] = vanillaGeometry,
                ["classic"] = classicGeometry,
                ["clear"] = clearGeometry,
                ["missing_node_fallback"] = compatibilityGeometry
            };
        }
        finally
        {
            ReleaseCard(brokenContractNode);
            ReleaseCard(clearNode);
            ReleaseCard(classicNode);
            ReleaseCard(vanillaNode);
        }
    }

    private static async Task<object> InspectMissingNodeFallback(
        Player player,
        CardModel model,
        NCard card,
        RuntimeAssertionCollector assertions)
    {
        await ApplyAffliction<Bound>(player, model);
        var overlay = RequireOverlay(card);
        var effectRoot = overlay.GetChild<Control>(0);
        var mask = effectRoot.GetNode<TextureRect>("card_mask");
        var border = effectRoot.GetNode<Control>("card_mask/border");
        SakuraCardMutationLedgers.For(card).Restore(SakuraCardRendererId.Classic);
        border.GetParent().RemoveChild(border);

        try
        {
            SakuraAfflictionVisualLayout.Apply(card, SakuraCardMutationLedgers.For(card));
            SakuraAfflictionVisualLayout.Apply(card, SakuraCardMutationLedgers.For(card));
            assertions.True(
                "affliction_missing_node_has_no_partial_mutation",
                Near(Vector2.Zero, overlay.Position)
                && Near(SakuraCardGeometry.VanillaLayoutSize * -0.5f, effectRoot.Position)
                && Near(SakuraCardGeometry.VanillaLayoutSize, mask.Size),
                $"Missing-node fallback mutated overlay geometry to {overlay.Position}/{effectRoot.Position}/{mask.Size}.");

            return new
            {
                overlay_position = Format(overlay.Position),
                effect_root_position = Format(effectRoot.Position),
                mask_size = Format(mask.Size)
            };
        }
        finally
        {
            border.QueueFree();
        }
    }

    private static async Task<Dictionary<string, object>> InspectAllAfflictions(
        Player player,
        CardModel model,
        NCard card,
        string layout,
        RuntimeAssertionCollector assertions)
    {
        var results = new Dictionary<string, object>(StringComparer.Ordinal);
        await InspectAffliction<Bound>("bound");
        await InspectAffliction<Entangled>("entangled");
        await InspectAffliction<Galvanized>("galvanized");
        await InspectAffliction<Hexed>("hexed");
        await InspectAffliction<Ringing>("ringing");
        await InspectAffliction<Smog>("smog");
        await InspectAffliction<Tainted>("tainted");
        CardCmd.ClearAffliction(model);
        assertions.Equal($"affliction_{layout}_cleared", null, CardOverlayField.GetValue(card));
        return results;

        async Task InspectAffliction<TAffliction>(string affliction)
            where TAffliction : AfflictionModel
        {
            CardCmd.ClearAffliction(model);
            await ApplyAffliction<TAffliction>(player, model);
            results[affliction] = InspectGeometry(layout, affliction, card, assertions);
            if (affliction == "bound")
            {
                card.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
                results["bound_refresh"] = InspectGeometry(
                    layout,
                    "bound_refresh",
                    card,
                    assertions);
            }
        }
    }

    private static object InspectGeometry(
        string layout,
        string affliction,
        NCard card,
        RuntimeAssertionCollector assertions)
    {
        var overlay = RequireOverlay(card);
        var mask = overlay.FindChild("card_mask", recursive: true, owned: false) as Control
            ?? throw new InvalidOperationException($"{layout} {affliction} overlay has no card_mask Control.");
        var faceRect = card.Body.GetGlobalRect();
        var maskRect = mask.GetGlobalRect();

        if (layout == "vanilla")
        {
            assertions.Equal(
                $"affliction_{layout}_{affliction}_overlay_position",
                Vector2.Zero,
                overlay.Position);
            assertions.Equal(
                $"affliction_{layout}_{affliction}_mask_size",
                SakuraCardGeometry.VanillaLayoutSize,
                mask.Size);
        }
        else
        {
            assertions.True(
                $"affliction_{layout}_{affliction}_mask_contained",
                Contains(faceRect, maskRect, GeometryTolerance),
                $"Card face {faceRect} does not contain Affliction mask {maskRect}.");

            var effectRoot = overlay.GetChild<Control>(0);
            assertions.True(
                $"affliction_{layout}_{affliction}_effect_root_matches_face",
                Near(effectRoot.Position, mask.Size * -0.5f)
                && Near(effectRoot.Size, mask.Size),
                $"Effect root {effectRoot.Position}/{effectRoot.Size} does not match mask {mask.Size}.");

            if (affliction == "entangled")
                AssertEntangled(layout, effectRoot, mask.Size, assertions);
            else if (affliction == "bound")
                AssertBound(layout, effectRoot, mask.Size, assertions);
            else if (affliction == "galvanized")
                AssertGalvanized(layout, effectRoot, mask.Size, assertions);
            else if (affliction == "hexed")
                AssertHexed(layout, effectRoot, mask.Size, assertions);
            else if (affliction == "ringing")
                AssertRinging(layout, effectRoot, mask.Size, assertions);
            else if (affliction == "smog")
                AssertSmog(layout, effectRoot, mask.Size, assertions);
            else if (affliction == "tainted")
                AssertTainted(layout, effectRoot, mask.Size, assertions);
        }

        return new
        {
            face = Format(faceRect),
            mask = Format(maskRect),
            overlay_position = Format(overlay.Position),
            overlay_size = Format(overlay.Size),
            nodes = SnapshotNodes(overlay)
        };
    }

    private static void AssertBound(
        string layout,
        Control root,
        Vector2 size,
        RuntimeAssertionCollector assertions)
    {
        var main = root.GetNode<Control>("vfx_container/main");
        var squareCenter = new Vector2(size.Y * 0.5f, size.Y * 0.5f);
        assertions.True(
            $"affliction_{layout}_bound_center_pivot",
            Near(main.Position, -squareCenter) && Near(main.PivotOffset, squareCenter),
            $"Bound main geometry was {main.Position}/{main.PivotOffset}.");
    }

    private static void AssertEntangled(
        string layout,
        Control root,
        Vector2 size,
        RuntimeAssertionCollector assertions)
    {
        var leaves = root.GetNode<Control>("vfx_container/leaves");
        var main = root.GetNode<Control>("vfx_container/main");
        assertions.True(
            $"affliction_{layout}_entangled_leaf_profile",
            Near(Vector2.Zero, leaves.Position)
            && Near(Vector2.Zero, main.PivotOffset)
            && Near(new Vector2(size.Y * -0.6f, size.Y * -0.6f), main.Position),
            $"Entangled leaves/main geometry was {leaves.Position}/{main.Position}/{main.PivotOffset}.");
    }

    private static void AssertGalvanized(
        string layout,
        Control root,
        Vector2 size,
        RuntimeAssertionCollector assertions)
    {
        var bottomRight = root.GetNode<Node2D>(
            "vfx_ui_card_affliction_lightning_corner_bottom_right");
        assertions.True(
            $"affliction_{layout}_galvanized_corner_profile",
            Near(size, bottomRight.Position),
            $"Galvanized bottom-right corner was {bottomRight.Position} instead of {size}.");
    }

    private static void AssertHexed(
        string layout,
        Control root,
        Vector2 size,
        RuntimeAssertionCollector assertions)
    {
        var bottom = root.GetNode<Node2D>("card_mask/vfx_common_specks_bottom");
        var expected = new Vector2(size.X * 0.5f, size.Y);
        assertions.True(
            $"affliction_{layout}_hexed_bottom_profile",
            Near(expected, bottom.Position),
            $"Hexed bottom specks were {bottom.Position} instead of {expected}.");
    }

    private static void AssertRinging(
        string layout,
        Control root,
        Vector2 size,
        RuntimeAssertionCollector assertions)
    {
        var left = root.GetNode<Control>("horns_left");
        var right = root.GetNode<Control>("horns_right");
        var widthRatio = size.X / SakuraCardGeometry.VanillaLayoutSize.X;
        var heightRatio = size.Y / SakuraCardGeometry.VanillaLayoutSize.Y;
        var expectedLeftCenter = new Vector2(5f * widthRatio, 211f * heightRatio);
        var expectedRightCenter = new Vector2(295f * widthRatio, 211f * heightRatio);
        assertions.True(
            $"affliction_{layout}_ringing_side_ornament_centers",
            Near(expectedLeftCenter, left.Position + left.PivotOffset)
            && Near(expectedRightCenter, right.Position + right.PivotOffset),
            $"Ringing side ornament centers were "
            + $"{left.Position + left.PivotOffset}/{right.Position + right.PivotOffset} instead of "
            + $"{expectedLeftCenter}/{expectedRightCenter}.");
    }

    private static void AssertSmog(
        string layout,
        Control root,
        Vector2 size,
        RuntimeAssertionCollector assertions)
    {
        var outer = root.GetNode<Control>("smog_main_outer");
        var squareCenter = new Vector2(size.Y * 0.5f, size.Y * 0.5f);
        assertions.True(
            $"affliction_{layout}_smog_outer_profile",
            Near(squareCenter, outer.PivotOffset)
            && Mathf.IsEqualApprox(outer.Position.X, (size.X - size.Y) * 0.5f),
            $"Smog outer geometry was {outer.Position}/{outer.PivotOffset}.");
    }

    private static void AssertTainted(
        string layout,
        Control root,
        Vector2 size,
        RuntimeAssertionCollector assertions)
    {
        var main = root.GetNode<Control>("vfx_container/main");
        assertions.True(
            $"affliction_{layout}_tainted_origin_profile",
            Near(Vector2.Zero, main.PivotOffset)
            && Near(new Vector2(size.Y * -0.6f, size.Y * -0.6f), main.Position),
            $"Tainted main geometry was {main.Position}/{main.PivotOffset}.");
    }

    private static string[] SnapshotNodes(Node root)
    {
        var nodes = new List<string>();
        Visit(root, root.Name.ToString());
        return [.. nodes];

        void Visit(Node node, string path)
        {
            var geometry = node switch
            {
                Control control => $" pos={Format(control.Position)} size={Format(control.Size)} scale={Format(control.Scale)} pivot={Format(control.PivotOffset)}",
                Node2D node2D => $" pos={Format(node2D.Position)} scale={Format(node2D.Scale)}",
                _ => string.Empty
            };
            nodes.Add($"{path} [{node.GetType().Name}]{geometry}");
            foreach (var child in node.GetChildren())
                Visit(child, $"{path}/{child.Name}");
        }
    }

    private static bool Contains(Rect2 outer, Rect2 inner, float tolerance) =>
        inner.Position.X >= outer.Position.X - tolerance
        && inner.Position.Y >= outer.Position.Y - tolerance
        && inner.End.X <= outer.End.X + tolerance
        && inner.End.Y <= outer.End.Y + tolerance;

    private static bool Near(Vector2 left, Vector2 right) =>
        left.DistanceTo(right) <= GeometryTolerance;

    private static string Format(Rect2 rect) =>
        $"({rect.Position.X:F2},{rect.Position.Y:F2}) {rect.Size.X:F2}x{rect.Size.Y:F2}";

    private static string Format(Vector2 vector) =>
        $"({vector.X:F2},{vector.Y:F2})";

    private static Task ApplyAffliction<TAffliction>(Player player, CardModel card)
        where TAffliction : AfflictionModel =>
        CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(
            player,
            async _ => await CardCmd.Afflict<TAffliction>(card, 1)));

    private static NCard CreateAttachedCard(CardModel model)
    {
        var card = NCard.Create(model)
            ?? throw new InvalidOperationException($"Could not create NCard for {model.Id}.");
        if (Engine.GetMainLoop() is not SceneTree tree)
            throw new InvalidOperationException("Godot main loop is not a SceneTree.");

        tree.Root.AddChild(card);
        card.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
        return card;
    }

    private static Control RequireOverlay(NCard card) =>
        CardOverlayField.GetValue(card) as Control
        ?? throw new InvalidOperationException($"{card.Model?.Id} did not create an Affliction overlay.");

    private static void ReleaseCard(NCard? card)
    {
        if (card is null)
            return;

        card.GetParent()?.RemoveChild(card);
        NodePool.Free(card);
    }
}
