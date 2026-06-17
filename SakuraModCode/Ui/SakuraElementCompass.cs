using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.TestSupport;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.Extensions;

namespace SakuraMod.SakuraModCode.Ui;

/// <summary>
/// Compass HUD mounted just above the energy orb that shows, per turn, how many times each
/// element (Wind / Water / Fire / Earth) has been played, with a needle pointing at the most
/// recently played element. The needle doubles as a readability aid for Talisman Combo, which
/// triggers the previous element's talisman effect.
///
/// Built entirely from built-in Godot nodes (no custom node type / scene), matching the project's
/// other VFX helpers. Element counts are read from the element powers via SakuraActions, so this
/// is a pure view: it never owns gameplay state.
/// </summary>
internal static class SakuraElementCompass
{
    // Layout. SizeScale grows the HUD by baking bigger pixel sizes into the geometry, so everything
    // renders natively at full resolution. (A transform scale would blur the text and icons.)
    // SizeScale and MountOffset are the two knobs to nudge in-game.
    private const float SizeScale = 1.6f;
    private const float RootSize = 112f * SizeScale;
    private const float SlotRadius = 33f * SizeScale;
    private const float DiscRadius = 14f * SizeScale;
    private const float NeedleLength = 19f * SizeScale;
    private const float InnerRingRadius = 11f * SizeScale;
    private const float StarRadius = 7f * SizeScale;
    private const int CountFontSize = 26;
    private const int CountOutlineSize = 5;
    private static readonly Vector2 RootCenter = new(RootSize * 0.5f, RootSize * 0.5f);

    // Position relative to EnergyCounterContainer: above the orb, shifted right.
    private static readonly Vector2 MountOffset = new(-14f, -168f);

    private static readonly Color RingColor = new(0.91f, 0.85f, 0.71f, 0.72f);   // pale champagne gold
    private static readonly Color InnerRingColor = new(0.85f, 0.92f, 0.98f, 0.32f);
    private static readonly Color StarColor = new(0.96f, 0.91f, 0.74f, 0.92f);
    private static readonly Color NeedleColor = new(1f, 0.96f, 0.84f, 1f);
    private static readonly Color DiscBgColor = new(0.07f, 0.10f, 0.13f, 0.80f);
    private static readonly Color BadgeBgColor = new(0.04f, 0.06f, 0.09f, 0.92f);
    private static readonly Color NumberColor = new(1f, 1f, 1f);
    private static readonly Color NumberOutlineColor = new(0.03f, 0.05f, 0.08f, 0.95f);

    private static readonly IReadOnlyList<SakuraElement> Elements = SakuraElementSets.AllElements;
    private static readonly Dictionary<string, Texture2D?> IconCache = [];

    private static CompassState? _active;
    private static Player? _trackedPlayer;
    private static NCombatUi? _mountedUi;

    public static void OnElementsPlayed(Player owner)
    {
        if (!IsTracking(owner) || _active is not { } compass)
            return;

        compass.Refresh();
        if (SakuraActions.TryGetLastPlayedElement(owner, out var element))
            compass.PointNeedleAt(element);
    }

    public static void OnTurnReset(Player owner)
    {
        if (!IsTracking(owner) || _active is not { } compass)
            return;

        compass.ResetCounts();
    }

    private static bool IsTracking(Player owner) =>
        ReferenceEquals(owner, _trackedPlayer)
        && _active is { } compass
        && GodotObject.IsInstanceValid(compass.Root)
        && compass.Root.IsInsideTree();

    private static void Mount(NCombatUi ui, CombatState state)
    {
        if (TestMode.IsOn || ui.EnergyCounterContainer is not { } container)
            return;

        var me = LocalContext.GetMe(state);
        if (me is null || !SakuraStarterCards.IsSakura(me))
            return;

        Unmount();

        var compass = Build(me);
        compass.Root.Position = MountOffset;
        var mount = EnergyCounterRoot(container) ?? container;
        mount.AddChildSafely(compass.Root);
        compass.Root.TreeExiting += () =>
        {
            if (ReferenceEquals(_active, compass))
            {
                compass.Dispose();
                _active = null;
                _trackedPlayer = null;
                _mountedUi = null;
            }
        };

        _active = compass;
        _trackedPlayer = me;
        _mountedUi = ui;
        compass.ResetCounts();
    }

    private static NEnergyCounter? EnergyCounterRoot(Control container) =>
        container.GetChildren().OfType<NEnergyCounter>().FirstOrDefault();

    private static void Unmount(NCombatUi? ui = null)
    {
        if (ui is not null && !ReferenceEquals(ui, _mountedUi))
            return;

        if (_active is { } compass)
        {
            compass.Dispose();
            if (GodotObject.IsInstanceValid(compass.Root) && !compass.Root.IsQueuedForDeletion())
                compass.Root.QueueFree();
        }

        _active = null;
        _trackedPlayer = null;
        _mountedUi = null;
    }

    private static CompassState Build(Player owner)
    {
        var root = new Control
        {
            Name = "SakuraElementCompass",
            Size = new Vector2(RootSize, RootSize),
            CustomMinimumSize = new Vector2(RootSize, RootSize),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };

        var draw = new Node2D { Name = "Draw", Position = RootCenter };
        root.AddChild(draw);

        draw.AddChild(BuildRing(SlotRadius, RingColor, 3.2f));
        draw.AddChild(BuildRing(InnerRingRadius, InnerRingColor, 2f));
        draw.AddChild(BuildStar(StarRadius, StarColor));

        var needle = BuildNeedle();
        draw.AddChild(needle);

        var slots = new Dictionary<SakuraElement, ElementSlot>();
        foreach (var element in Elements)
            slots[element] = BuildSlot(root, draw, element);

        return new CompassState(owner, root, needle, slots);
    }

    private static Line2D BuildRing(float radius, Color color, float width)
    {
        return new Line2D
        {
            Points = CirclePoints(radius, 48),
            DefaultColor = color,
            Width = width,
            Closed = true,
            Antialiased = true
        };
    }

    private static Polygon2D BuildStar(float radius, Color color)
    {
        var points = new Vector2[8];
        for (var i = 0; i < 8; i++)
        {
            var angle = Mathf.Pi * i / 4f - Mathf.Pi / 2f;
            var r = i % 2 == 0 ? radius : radius * 0.42f;
            points[i] = new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r);
        }

        return new Polygon2D { Polygon = points, Color = color };
    }

    private static Polygon2D BuildNeedle()
    {
        // Drawn pointing along +X (rotation 0 == East); rotated per element.
        return new Polygon2D
        {
            Name = "Needle",
            Polygon =
            [
                new Vector2(0f, -3.8f),
                new Vector2(0f, 3.8f),
                new Vector2(NeedleLength, 0f)
            ],
            Color = NeedleColor,
            Modulate = new Color(1f, 1f, 1f, 0f)
        };
    }

    private static ElementSlot BuildSlot(Control root, Node2D draw, SakuraElement element)
    {
        var offset = SlotOffset(element);
        var color = ColorFor(element);
        var badgeLocal = offset + new Vector2(DiscRadius * 0.66f, DiscRadius * 0.66f);

        var slotNode = new Node2D { Name = $"Slot_{element}", Position = offset };
        draw.AddChild(slotNode);

        slotNode.AddChild(BuildFilledDisc(DiscRadius, DiscBgColor));
        var outline = BuildDiscOutline(DiscRadius, color, width: 3.2f);
        slotNode.AddChild(outline);

        var icon = new Sprite2D { Name = "Icon", Texture = IconFor(element) };
        ScaleIconToFit(icon, DiscRadius * 1.5f);
        slotNode.AddChild(icon);

        // Dark chip behind the number, parented to the draw layer (static, like the label) so the
        // white count reads clearly over the icon art regardless of element color.
        var badgeBg = BuildFilledDisc(CountFontSize * 0.6f, BadgeBgColor);
        badgeBg.Position = badgeLocal;
        draw.AddChild(badgeBg);

        var label = new Label
        {
            Name = $"Count_{element}",
            Text = string.Empty,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Size = new Vector2(CountFontSize * 1.4f, CountFontSize * 1.2f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        label.Position = RootCenter + badgeLocal - label.Size * 0.5f;
        label.AddThemeFontSizeOverride("font_size", CountFontSize);
        label.AddThemeColorOverride("font_color", NumberColor);
        label.AddThemeColorOverride("font_outline_color", NumberOutlineColor);
        label.AddThemeConstantOverride("outline_size", CountOutlineSize);
        root.AddChild(label);

        return new ElementSlot(slotNode, outline, icon, label, badgeBg, color);
    }

    private static Polygon2D BuildFilledDisc(float radius, Color color) =>
        new() { Polygon = CirclePoints(radius, 28), Color = color };

    private static Line2D BuildDiscOutline(float radius, Color color, float width) =>
        new()
        {
            Points = CirclePoints(radius, 28),
            DefaultColor = color,
            Width = width,
            Closed = true,
            Antialiased = true
        };

    private static Vector2[] CirclePoints(float radius, int pointCount)
    {
        var points = new Vector2[pointCount];
        for (var i = 0; i < pointCount; i++)
        {
            var angle = Mathf.Tau * i / pointCount;
            points[i] = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
        }

        return points;
    }

    private static void ScaleIconToFit(Sprite2D icon, float targetSize)
    {
        var size = icon.Texture?.GetSize() ?? Vector2.Zero;
        var longest = Mathf.Max(size.X, size.Y);
        if (longest > 0f)
            icon.Scale = Vector2.One * (targetSize / longest);
    }

    private static Texture2D? IconFor(SakuraElement element)
    {
        // Use the 256px "big" icons so they stay crisp at HUD size.
        var path = IconFileName(element).BigPowerImagePath();
        if (IconCache.TryGetValue(path, out var cached))
            return cached;

        var texture = ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
        IconCache[path] = texture;
        return texture;
    }

    private static string IconFileName(SakuraElement element) =>
        element switch
        {
            SakuraElement.Wind => "wind_element.png",
            SakuraElement.Water => "water_element.png",
            SakuraElement.Fire => "fire_element.png",
            SakuraElement.Earth => "earth_element.png",
            _ => "power.png"
        };

    // Wind = N (up), Fire = E (right), Earth = S (down), Water = W (left).
    // Opposites face each other: Wind <-> Earth (vertical), Fire <-> Water (horizontal).
    private static Vector2 SlotOffset(SakuraElement element) =>
        element switch
        {
            SakuraElement.Wind => new Vector2(0f, -SlotRadius),
            SakuraElement.Fire => new Vector2(SlotRadius, 0f),
            SakuraElement.Earth => new Vector2(0f, SlotRadius),
            SakuraElement.Water => new Vector2(-SlotRadius, 0f),
            _ => Vector2.Zero
        };

    private static float AngleFor(SakuraElement element) =>
        element switch
        {
            SakuraElement.Wind => -Mathf.Pi / 2f,
            SakuraElement.Fire => 0f,
            SakuraElement.Earth => Mathf.Pi / 2f,
            SakuraElement.Water => Mathf.Pi,
            _ => 0f
        };

    private static Color ColorFor(SakuraElement element) =>
        element switch
        {
            SakuraElement.Wind => new Color("6FE0B0"),
            SakuraElement.Water => new Color("5FB8F0"),
            SakuraElement.Fire => new Color("FF8A6B"),
            SakuraElement.Earth => new Color("E6C36A"),
            _ => Colors.White
        };

    private sealed class ElementSlot(
        Node2D node,
        Line2D outline,
        Sprite2D icon,
        Label count,
        Polygon2D badgeBg,
        Color color)
    {
        public Node2D Node { get; } = node;
        public Line2D Outline { get; } = outline;
        public Sprite2D Icon { get; } = icon;
        public Label Count { get; } = count;
        public Polygon2D BadgeBg { get; } = badgeBg;
        public Color Color { get; } = color;
        public int Value { get; set; }
    }

    private sealed class CompassState : IDisposable
    {
        private readonly Player _owner;
        private readonly Polygon2D _needle;
        private readonly Dictionary<SakuraElement, ElementSlot> _slots;
        private Tween? _needleTween;
        private bool _disposed;

        public CompassState(
            Player owner,
            Control root,
            Polygon2D needle,
            Dictionary<SakuraElement, ElementSlot> slots)
        {
            _owner = owner;
            Root = root;
            _needle = needle;
            _slots = slots;
        }

        public Control Root { get; }

        public void Refresh()
        {
            foreach (var (element, slot) in _slots)
            {
                var value = SakuraActions.PlayedElementCount(_owner, element);
                var increased = value > slot.Value;
                slot.Value = value;
                ApplySlotVisual(slot, value);
                if (increased)
                    Pulse(slot);
            }
        }

        public void ResetCounts()
        {
            foreach (var slot in _slots.Values)
            {
                slot.Value = 0;
                ApplySlotVisual(slot, 0);
            }

            FadeNeedle(0f);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            KillNeedleTween();
            _disposed = true;
        }

        public void PointNeedleAt(SakuraElement element)
        {
            if (!Root.IsInsideTree())
                return;

            _needle.Rotation = NormalizeToward(_needle.Rotation, AngleFor(element));
            KillNeedleTween();
            _needleTween = Root.CreateTween().SetParallel();
            _needleTween.TweenProperty(_needle, "rotation", AngleFor(element), 0.2f)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Back);
            _needleTween.TweenProperty(_needle, "modulate:a", 1f, 0.16f)
                .SetEase(Tween.EaseType.Out);
        }

        private void FadeNeedle(float alpha)
        {
            if (!Root.IsInsideTree())
            {
                _needle.Modulate = new Color(1f, 1f, 1f, alpha);
                return;
            }

            KillNeedleTween();
            _needleTween = Root.CreateTween();
            _needleTween.TweenProperty(_needle, "modulate:a", alpha, 0.2f).SetEase(Tween.EaseType.In);
        }

        private void KillNeedleTween()
        {
            if (_needleTween is { } tween && tween.IsValid())
                tween.Kill();
            _needleTween = null;
        }

        private static void ApplySlotVisual(ElementSlot slot, int value)
        {
            var lit = value > 0;
            slot.Count.Text = lit ? value.ToString() : string.Empty;
            slot.Count.Visible = lit;
            slot.BadgeBg.Visible = lit;
            slot.Icon.Modulate = new Color(1f, 1f, 1f, lit ? 1f : 0.4f);
            slot.Outline.DefaultColor = lit
                ? slot.Color
                : new Color(slot.Color.R, slot.Color.G, slot.Color.B, 0.35f);
        }

        private void Pulse(ElementSlot slot)
        {
            if (!Root.IsInsideTree())
                return;

            var tween = Root.CreateTween();
            tween.TweenProperty(slot.Node, "scale", Vector2.One * 1.28f, 0.1f)
                .From(Vector2.One)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Back);
            tween.TweenProperty(slot.Node, "scale", Vector2.One, 0.14f)
                .SetEase(Tween.EaseType.InOut)
                .SetTrans(Tween.TransitionType.Sine);
        }

        // Keep the tween's start rotation within +/-pi of the target so it spins the short way.
        private static float NormalizeToward(float current, float target)
        {
            while (current - target > Mathf.Pi)
                current -= Mathf.Tau;
            while (target - current > Mathf.Pi)
                current += Mathf.Tau;
            return current;
        }
    }

    [HarmonyPatch(typeof(NCombatUi), nameof(NCombatUi.Activate))]
    private static class MountPatch
    {
        [HarmonyPostfix]
        private static void ActivatePostfix(NCombatUi __instance, CombatState state) =>
            Mount(__instance, state);
    }

    [HarmonyPatch(typeof(NCombatUi), nameof(NCombatUi.AnimOut))]
    private static class AnimOutPatch
    {
        [HarmonyPrefix]
        private static void AnimOutPrefix(NCombatUi __instance) =>
            Unmount(__instance);
    }

    [HarmonyPatch(typeof(NCombatUi), nameof(NCombatUi.Deactivate))]
    private static class DeactivatePatch
    {
        [HarmonyPrefix]
        private static void DeactivatePrefix(NCombatUi __instance) =>
            Unmount(__instance);
    }
}
