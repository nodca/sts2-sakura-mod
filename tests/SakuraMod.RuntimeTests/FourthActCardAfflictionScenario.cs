using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Afflictions;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Pooling;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.FourthAct.Earth.CardState;
using SakuraMod.SakuraModCode.FourthAct.Earth.Powers;
using SakuraMod.SakuraModCode.FourthAct.Water.CardState;
using SakuraMod.SakuraModCode.FourthAct.Water.Powers;
using SakuraMod.SakuraModCode.FourthAct.Wind.CardState;
using SakuraMod.TestProtocol;
using System.Reflection;

namespace SakuraMod.RuntimeTests;

internal static class FourthActCardAfflictionScenario
{
    private const float Tolerance = 0.6f;
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

        // Buried: Landslide always buries; the card survives one shuffle in the Discard Pile.
        var buried = combat.CreateCard<ClowShield>(player);
        var replaced = combat.CreateCard<ClowShield>(player);
        var plain = combat.CreateCard<ClowFlower>(player);
        await Fixture(player, async _ =>
        {
            foreach (var card in new CardModel[] { plain, replaced, buried })
                await CardPileCmd.Add(card, PileType.Discard);
            await CardCmd.Afflict<SleepingAffliction>(replaced, 1);
            await EarthCombatRules.MarkBuried(buried);
            await EarthCombatRules.MarkBuried(replaced);
        });
        assertions.True("buried_marks_card", buried.Affliction is BuriedAffliction);
        assertions.True("buried_replaces_foreign_affliction", replaced.Affliction is BuriedAffliction);
        await Fixture(player, choice => CardPileCmd.Shuffle(choice, player));
        assertions.Equal("buried_stays_in_discard", PileType.Discard, buried.Pile?.Type);
        assertions.True("buried_released_after_shuffle", buried.Affliction is null, $"{buried.Affliction?.Id}");
        assertions.Equal("unburied_card_shuffled_into_draw", PileType.Draw, plain.Pile?.Type);

        // Frozen: a projection of the Power on every hand card, never a second play gate.
        var handCard = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowShield>(combat, player);
        var leaving = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowShield>(combat, player);
        var foreign = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowFlower>(combat, player);
        await Fixture(player, async choice =>
        {
            await CardCmd.Afflict<Bound>(foreign, 1);
            await PowerCmd.Apply<WaterFrozenPower>(choice, player.Creature, 1, combat.Enemies.First(), null);
        });
        assertions.True("frozen_projects_onto_hand", handCard.Affliction is FrozenAffliction && leaving.Affliction is FrozenAffliction);
        assertions.True("frozen_keeps_foreign_affliction", foreign.Affliction is Bound);
        assertions.True("frozen_power_blocks_manual_play", !handCard.CanPlay());
        var arriving = await CombatScenarioContext.AddGeneratedCardToHandAsync<ClowFlower>(combat, player);
        assertions.True("frozen_projects_onto_new_hand_card", arriving.Affliction is FrozenAffliction);
        await Fixture(player, _ => CardPileCmd.Add(leaving, PileType.Discard));
        assertions.True("frozen_clears_when_leaving_hand", leaving.Affliction is null, $"{leaving.Affliction?.Id}");
        await Fixture(player, _ => PowerCmd.Remove(player.Creature.GetPower<WaterFrozenPower>()!));
        assertions.True("frozen_clears_on_power_removal",
            player.PlayerCombatState!.AllCards.All(static card => card.Affliction is not FrozenAffliction));
        assertions.True("frozen_removal_keeps_foreign_affliction", foreign.Affliction is Bound);
        assertions.True("unfrozen_card_playable", handCard.CanPlay());

        // Overlay geometry on the native face and both Sakura faces.
        var geometry = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (layout, model) in new (string, CardModel)[]
                 {
                     ("vanilla", combat.CreateCard<DefendIronclad>(player)),
                     ("classic", combat.CreateCard<ClowShield>(player)),
                     ("clear", combat.CreateCard<Repair>(player)),
                 })
        {
            var node = CreateAttachedCard(model);
            try
            {
                geometry[$"{layout}_sleeping"] = await InspectOverlay<SleepingAffliction>(player, model, node, layout, assertions);
                geometry[$"{layout}_buried"] = await InspectOverlay<BuriedAffliction>(player, model, node, layout, assertions);
                geometry[$"{layout}_frozen"] = await InspectOverlay<FrozenAffliction>(player, model, node, layout, assertions);
                CardCmd.ClearAffliction(model);
                assertions.Equal($"{layout}_overlay_cleared", null, CardOverlayField.GetValue(node));
            }
            finally
            {
                node.GetParent()?.RemoveChild(node);
                NodePool.Free(node);
            }
        }

        return new Dictionary<string, object?>
        {
            ["fixture"] = "Buried via EarthCombatRules + native Shuffle; Frozen via WaterFrozenPower on hand cards; overlays on DefendIronclad, ClowShield, Repair.",
            ["geometry"] = geometry
        };
    }

    private static async Task<object> InspectOverlay<TAffliction>(
        Player player,
        CardModel model,
        NCard card,
        string layout,
        RuntimeAssertionCollector assertions)
        where TAffliction : AfflictionModel
    {
        CardCmd.ClearAffliction(model);
        await Fixture(player, async _ => await CardCmd.Afflict<TAffliction>(model, 1));
        card.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
        await card.AwaitProcessFrame();
        var name = $"{layout}_{typeof(TAffliction).Name}";
        var overlay = CardOverlayField.GetValue(card) as Control
            ?? throw new InvalidOperationException($"{name} created no overlay.");
        var frame = overlay.GetNodeOrNull<Control>(SakuraAfflictionVisualLayout.ModOverlayFrameName)
            ?? throw new InvalidOperationException($"{name} overlay has no Frame.");
        var frameRect = frame.GetGlobalRect();
        if (layout == "vanilla")
        {
            assertions.True($"{name}_native_frame_size", Near(SakuraCardGeometry.VanillaLayoutSize, frame.Size), $"{frame.Size}");
            assertions.True($"{name}_native_frame_centered", Near(card.GlobalPosition, frameRect.GetCenter()),
                $"frame centre {frameRect.GetCenter()} vs card {card.GlobalPosition}");
        }
        else
        {
            var face = card.Body.GetGlobalRect();
            assertions.True($"{name}_frame_matches_face", Near(face.Position, frameRect.Position) && Near(face.Size, frameRect.Size),
                $"frame {frameRect} vs face {face}");
        }

        return new { position = frameRect.Position.ToString(), size = frameRect.Size.ToString() };
    }

    private static Task Fixture(Player player, Func<PlayerChoiceContext, Task> body) =>
        CombatScenarioContext.EnqueueAndWaitAsync(new RuntimeFixtureAction(player, body));

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

    private static bool Near(Vector2 expected, Vector2 actual) =>
        Mathf.Abs(expected.X - actual.X) <= Tolerance && Mathf.Abs(expected.Y - actual.Y) <= Tolerance;
}
