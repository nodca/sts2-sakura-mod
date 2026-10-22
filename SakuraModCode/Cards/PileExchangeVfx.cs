using Godot;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.TestSupport;
using STS2RitsuLib.CardPiles.Nodes;

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>Which card's motif a pile exchange shows.</summary>
public enum PileExchangeMotif
{
    /// <summary>Exchange: two flowers trade places along a vine.</summary>
    Exchange,
    /// <summary>Reversal: a diagonally split card plate turns negative.</summary>
    Reversal
}

/// <summary>One drawn motif of a pile exchange, advanced on the exchange's clock.</summary>
internal interface IPileExchangeMotif
{
    /// <summary>Where the motif centres, in its root's local space.</summary>
    Vector2 Focus { get; }

    void Advance(float t);
}

/// <summary>
/// Exchange and Reversal swap two whole piles without moving card nodes, so this
/// is what the player sees of the swap. Each card shows the motif of its own
/// card face over the two pile buttons, while the cards themselves fly as the
/// game's own shuffle souls. Local player only, fire-and-forget: nothing here
/// waits or throws into the swap.
/// </summary>
public static class PileExchangeVfx
{
    internal const float Duration = 1.15f;
    internal const float FadeFrom = 0.88f;
    private const float SoulsFrom = 0.08f;
    // The shuffle's own spacing, capped so a huge pile does not flood the HUD.
    private const int MaxSouls = 12;
    private const float SoulSpacing = 0.045f;
    private const float SoulBudget = 0.4f;
    private const int VfxZIndex = 3000;
    internal static readonly Color MemoryColor = new(0.58f, 0.93f, 1f, 0.82f);

    private static Node2D? _activeRoot;
    private static ActiveExchange _active;
    private static bool _failureLogged;

    /// <summary>Where the exchange now playing for the local player lies, in global coordinates.</summary>
    internal readonly record struct ActiveExchange(Vector2 First, Vector2 Second, Vector2 Focus);

    public static void Play(
        Player owner,
        PileType firstPileType,
        PileType secondPileType,
        int firstCount,
        int secondCount,
        PileExchangeMotif motif = PileExchangeMotif.Exchange)
    {
        Shader? plateShader = null;
        if (TestMode.IsOn
            || !SakuraModConfig.IsCardVfxEnabled()
            || !LocalContext.IsMe(owner)
            || firstCount + secondCount == 0
            || NCombatRoom.Instance is not { Ui: { } ui } room
            || !GodotObject.IsInstanceValid(ui)
            || motif == PileExchangeMotif.Reversal && (plateShader = ReversalPlateMotif.TryGetShader()) is null
            || !TryResolveEndpoint(room, firstPileType, out var firstCenter)
            || !TryResolveEndpoint(room, secondPileType, out var secondCenter)
            || firstCenter.DistanceSquaredTo(secondCenter) <= 1f)
        {
            return;
        }

        try
        {
            var midpoint = (firstCenter + secondCenter) * 0.5f;
            var root = new Node2D
            {
                Name = motif == PileExchangeMotif.Reversal ? "SakuraReversalPlatesVfx" : "SakuraExchangeVineVfx",
                ZIndex = VfxZIndex,
                ZAsRelative = false
            };
            room.Ui!.AddChildSafely(root);
            root.GlobalPosition = midpoint;

            var first = firstCenter - midpoint;
            var second = secondCenter - midpoint;
            var screenCentre = room.GetViewportRect().Size * 0.5f - midpoint;
            IPileExchangeMotif drawn = motif == PileExchangeMotif.Reversal
                ? ReversalPlateMotif.Create(root, plateShader!, first, second, screenCentre.X)
                : ExchangeVineMotif.Create(root, first, second, screenCentre);
            Track(root, new ActiveExchange(firstCenter, secondCenter, midpoint + drawn.Focus));

            var tween = root.CreateTween().SetParallel();
            tween.TweenMethod(Callable.From<float>(drawn.Advance), 0f, Duration, Duration);
            tween.Finished += root.QueueFreeSafely;
            LaunchSouls(root, owner, firstPileType, secondPileType, firstCount);
            LaunchSouls(root, owner, secondPileType, firstPileType, secondCount);
        }
        catch (Exception exception)
        {
            if (_failureLogged)
                return;
            _failureLogged = true;
            MainFile.Logger.Error($"Pile exchange VFX failed: {exception}");
        }
    }

    /// <summary>The exchange now playing for <paramref name="owner"/>, if any; the Loop hangs its count on it.</summary>
    internal static bool TryGetActiveExchange(Player owner, out ActiveExchange exchange)
    {
        exchange = _active;
        return LocalContext.IsMe(owner)
            && _activeRoot is { } root
            && GodotObject.IsInstanceValid(root)
            && root.IsInsideTree();
    }

    /// <summary>Fades everything out over the last stretch of the exchange.</summary>
    internal static float FadeAt(float t) =>
        1f - Math.Clamp((t - FadeFrom) / (Duration - FadeFrom), 0f, 1f);

    /// <summary>Pops step on the 12 Hz cel clock: four frames to full size.</summary>
    internal static float Stepped(float u) =>
        u < 1f ? MathF.Floor(u * 4f) / 4f : 1f;

    internal static bool TryGetPileCenter(NCombatRoom room, PileType pileType, out Vector2 center)
    {
        center = default;
        if (room.Ui is null)
            return false;

        return TryResolveEndpoint(room, pileType, out center);
    }

    private static bool TryResolveEndpoint(NCombatRoom room, PileType pileType, out Vector2 center)
    {
        var ui = room.Ui!;
        if (pileType == SakuraMemoryPile.PileType)
        {
            center = FindMemoryPileButton(room) is { } memoryButton
                ? CenterOf(memoryButton)
                : CenterOf(ui.ExhaustPile) + SakuraMemoryPile.UiOffsetAboveExhaust;
            return true;
        }

        Control? control = pileType switch
        {
            PileType.Draw => ui.DrawPile,
            PileType.Discard => ui.DiscardPile,
            PileType.Exhaust => ui.ExhaustPile,
            _ => null
        };
        center = control is null ? default : CenterOf(control);
        return control is not null;
    }

    private static void Track(Node2D root, ActiveExchange exchange)
    {
        _activeRoot = root;
        _active = exchange;
        root.TreeExiting += () =>
        {
            if (ReferenceEquals(_activeRoot, root))
                _activeRoot = null;
        };
    }

    // The game's own shuffle soul for each card, spaced like a shuffle. The
    // souls belong to the combat VFX container and clean themselves up; the
    // root's tween only schedules them, so a root freed early stops the rest.
    private static void LaunchSouls(Node2D root, Player owner, PileType from, PileType to, int cards)
    {
        var count = Math.Min(cards, MaxSouls);
        if (count == 0
            || CardPile.Get(from, owner) is not { } fromPile
            || CardPile.Get(to, owner) is not { } toPile
            || owner.Creature.GetVfxContainer() is not { } container)
        {
            return;
        }

        var trailPath = owner.Character.TrailPath;
        var spacing = Math.Min(SoulSpacing, SoulBudget / count);
        var tween = root.CreateTween().SetParallel();
        for (var i = 0; i < count; i++)
        {
            tween.TweenCallback(Callable.From(() =>
                {
                    if (GodotObject.IsInstanceValid(container)
                        && NCardFlyShuffleVfx.Create(fromPile, toPile, trailPath) is { } soul)
                    {
                        container.AddChildSafely(soul);
                    }
                }))
                .SetDelay(SoulsFrom + i * spacing);
        }
    }

    private static Vector2 CenterOf(Control control) =>
        control.GlobalPosition + new Vector2(control.Size.X * control.Scale.X, control.Size.Y * control.Scale.Y) * 0.5f;

    private static NModCardPileButton? FindMemoryPileButton(NCombatRoom room) =>
        room.Ui?.FindChildren("*", recursive: true, owned: false)
            .OfType<NModCardPileButton>()
            .FirstOrDefault(button => button.Definition?.PileType == SakuraMemoryPile.PileType);
}
