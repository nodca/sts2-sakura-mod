using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>
/// Clow Time's hourglass: it draws in front of Sakura with its sand run out,
/// turns over and pours; on the halt the stream stops in mid-air and Stasis
/// lands. Then the glass breaks and its sand flies onto what Stasis keeps —
/// Block, energy, the hand and element states.
/// </summary>
internal sealed class ClowTimeHourglassVfx : CelVfxSession
{
    internal const string ShaderPath = MainFile.ResPath + "/shaders/card_vfx/clow_time_hourglass.gdshader";
    internal const string SandRootName = "SakuraClowTimeSand";
    internal static IReadOnlyList<string> AssetPaths { get; } = [ShaderPath];

    private const int VfxZIndex = 1;
    // Above the hand and energy UI, like Transparent Time's seals.
    private const int SandZIndex = 3000;
    private const float Height = 150f;
    private static readonly Vector2 Canvas = new(320f, 360f);
    private static readonly Color SandLight = new(1.00f, 0.84f, 0.40f);
    private static readonly Color SandMid = new(0.94f, 0.66f, 0.22f);
    private static readonly Color WaterGlint = new(0.62f, 0.88f, 1.00f);

    private static ClowTimeHourglassVfx? _current;
    private static bool _loadFailureLogged;

    private readonly Creature _caster;
    private readonly NCreature? _casterNode;
    private readonly Node2D _sandRoot;
    private readonly ColorRect _glass;
    private readonly ShaderMaterial _material;
    private readonly List<Grain> _grains = [];
    private readonly Random _random = new();

    private float _t;
    private float _end = Timeline.ScatterAt + Timeline.Shatter;
    private bool _bodyStarted;
    private bool _scattered;
    private bool _outro;

    private sealed class Grain
    {
        internal required Polygon2D Body { get; init; }
        internal required Polygon2D Flick { get; init; }
        internal required string Kind { get; init; }
        internal required Control Target { get; init; }
        internal required Vector2 Lift { get; init; }
        internal required float Launch { get; init; }
        internal Vector2 From;
        internal Vector2 LastAim;
        internal bool Lost;
    }

    /// <summary>Beat times in seconds after the prelude.</summary>
    internal static class Timeline
    {
        internal const float Form = 0.25f;
        internal const float Flip = 0.20f;
        internal const float Pour = 0.30f;
        internal const float HaltAt = Form + Flip + Pour;
        internal const float HaltFlash = 0.25f;
        // The frozen stream holds for a beat before the glass breaks.
        internal const float ScatterAt = HaltAt + 0.30f;
        internal const float Shatter = 0.45f;
        internal const float GrainStagger = 0.02f;
        internal const float GrainFlight = 0.45f;
        internal const float Flick = 0.20f;
        // The pour stops part way: the halt catches the sand mid-fall.
        internal const float PourShare = 0.45f;
    }

    private ClowTimeHourglassVfx(Node2D root, NCombatRoom room, Creature caster, NCreature? casterNode,
        Node2D sandRoot, ColorRect glass, ShaderMaterial material)
        : base(root, room)
    {
        _caster = caster;
        _casterNode = casterNode;
        _sandRoot = sandRoot;
        _glass = glass;
        _material = material;
        // Every exit path (outro, lifetime cap, combat end, tree exit) frees the
        // root, so the sand and the current-session slot are released there.
        root.TreeExiting += ReleaseExtras;
    }

    protected override IEnumerable<ShaderMaterial> Materials => [_material];

    // Prelude, the pour, the scatter and every grain's landing.
    protected override float MaximumLifetime => 5.0f;

    // --- Orchestration ----------------------------------------------------------

    internal static Task PlayOrResolveAsync(CardModel card, Creature caster, Func<Cues, Task> resolveGameplay)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(caster);
        ArgumentNullException.ThrowIfNull(resolveGameplay);

        return CelVfxSession.PlayOrResolveAsync(
            "Clow Time hourglass",
            () => TryCreate(caster),
            session => session.PlayPrelude(card, caster),
            scope => resolveGameplay(new Cues(scope)),
            session => session.FinishAfterScatter(),
            session => session.Dispose());
    }

    internal sealed class Cues(CueScope<ClowTimeHourglassVfx> scope)
    {
        /// <summary>
        /// Waits until the falling sand stops in mid-air. Await immediately before Stasis.
        /// </summary>
        /// <remarks>Returns at once when presentation is off, failed, or already past the halt.</remarks>
        internal Task HaltAsync() =>
            scope.InvokeAsync("halt", static session => session.WaitUntilAsync(Timeline.HaltAt));
    }

    private static ClowTimeHourglassVfx? TryCreate(Creature caster)
    {
        if (!TryPrepare("Clow Time hourglass", LoadShader, out var room, out _, out var shader))
            return null;

        // One hourglass at a time: a repeat play (Twin) replaces the previous one.
        _current?.Dispose();

        Node2D? root = null;
        Node2D? sandRoot = null;
        try
        {
            var casterNode = room.GetCreatureNode(caster);
            root = new Node2D
            {
                Name = "SakuraClowTimeHourglassVfx",
                ZAsRelative = false,
                ZIndex = VfxZIndex
            };
            room.CombatVfxContainer.AddChildSafely(root);
            root.GlobalPosition = HourglassCentre(room, casterNode);

            var material = new ShaderMaterial { Shader = shader };
            var glass = new ColorRect
            {
                Name = "Hourglass",
                Color = Colors.White,
                Material = material,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Visible = false,
                Size = Canvas,
                Position = -Canvas * 0.5f
            };
            root.AddChild(glass);
            material.SetShaderParameter("region_size", Canvas);
            material.SetShaderParameter("height", Height);
            foreach (var uniform in new[] { "form", "flip", "pour", "halted", "halt_flash", "shatter" })
                material.SetShaderParameter(uniform, 0f);

            sandRoot = new Node2D
            {
                Name = SandRootName,
                ZAsRelative = false,
                ZIndex = SandZIndex
            };
            room.Ui.AddChildSafely(sandRoot);

            var session = new ClowTimeHourglassVfx(root, room, caster, casterNode, sandRoot, glass, material);
            _current = session;
            // Started after construction, never inside it: the base clock pulls
            // Materials, and during a base constructor the subclass fields are empty.
            session.StartClock();
            return session;
        }
        catch (Exception exception)
        {
            LogLoadFailure(exception);
            sandRoot?.QueueFreeSafely();
            root?.QueueFreeSafely();
            return null;
        }
    }

    // In front of Sakura and a little above her head line, toward the enemies.
    private static Vector2 HourglassCentre(NCombatRoom room, NCreature? casterNode)
    {
        if (CelVfxGeometry.ResolveCaster(casterNode) is { } anchor)
            return anchor.BodyCenter + new Vector2(anchor.FacingSign * anchor.BodySize.X * 1.05f,
                -anchor.BodySize.Y * 0.22f);
        var view = room.CombatVfxContainer.GetViewportRect();
        return view.Position + view.Size * new Vector2(0.32f, 0.45f);
    }

    private async Task<bool> PlayPrelude(CardModel card, Creature? caster)
    {
        if (!await PlayCelPrelude(card, caster))
            return false;

        StartBody();
        return IsActive();
    }

    private void StartBody()
    {
        if (_bodyStarted || !IsActive())
            return;

        _bodyStarted = true;
        _glass.Visible = true;
        Advance(0f);
        RunBody(0f, _end);
    }

    // One tween owns body time; configured fully before any yield. The scatter
    // extends it once the grains are known.
    private void RunBody(float from, float to)
    {
        var body = Track(Root.CreateTween());
        body.TweenMethod(Callable.From<float>(Advance), from, to, to - from);
        body.TweenCallback(Callable.From(OnBodyEnd));
    }

    private void OnBodyEnd()
    {
        if (_t + 0.001f < _end)
        {
            RunBody(_t, _end);
            return;
        }
        if (_outro)
            Dispose();
    }

    // --- Cues -------------------------------------------------------------------

    private async Task WaitUntilAsync(float at)
    {
        if (!_bodyStarted || !IsActive())
            return;

        var wait = at - _t;
        if (wait > 0f)
            await WaitActive(wait);
    }

    // --- Frame ------------------------------------------------------------------

    private void Advance(float t)
    {
        _t = t;
        _material.SetShaderParameter("form", Math.Clamp(t / Timeline.Form, 0f, 1f));
        _material.SetShaderParameter("flip", Math.Clamp((t - Timeline.Form) / Timeline.Flip, 0f, 1f));
        _material.SetShaderParameter("pour",
            Timeline.PourShare * Math.Clamp((t - Timeline.Form - Timeline.Flip) / Timeline.Pour, 0f, 1f));
        _material.SetShaderParameter("halted", t >= Timeline.HaltAt ? 1f : 0f);
        _material.SetShaderParameter("halt_flash",
            t >= Timeline.HaltAt ? Math.Max(0f, 1f - (t - Timeline.HaltAt) / Timeline.HaltFlash) : 0f);
        _material.SetShaderParameter("shatter", Math.Clamp((t - Timeline.ScatterAt) / Timeline.Shatter, 0f, 1f));

        if (!_scattered && t >= Timeline.ScatterAt)
            Scatter();
        UpdateGrains(t);
    }

    private void Scatter()
    {
        _scattered = true;
        var targets = SakuraPreservedStateTargets.Resolve(Room, _casterNode, _caster);
        var launch = Timeline.ScatterAt;
        foreach (var group in targets)
        {
            // A single display gets a handful; the hand gets three per card.
            var perNode = group.Nodes.Count == 1 ? 6 : 3;
            foreach (var node in group.Nodes.Take(group.Kind == "hand" ? 6 : 1))
            {
                for (var i = 0; i < perNode; i++)
                {
                    _grains.Add(CreateGrain(group.Kind, node, launch));
                    launch += Timeline.GrainStagger;
                }
            }
        }
        _end = Math.Max(_end, launch + Timeline.GrainFlight + Timeline.Flick);
    }

    private Grain CreateGrain(string kind, Control target, float launch)
    {
        var size = 4.5f + (float)_random.NextDouble() * 2.5f;
        var body = new Polygon2D
        {
            Polygon = [new(0f, -size), new(size * 0.8f, 0f), new(0f, size), new(-size * 0.8f, 0f)],
            Color = _random.NextDouble() < 0.18 ? WaterGlint : (_random.NextDouble() < 0.5 ? SandLight : SandMid),
            Visible = false
        };
        var flick = new Polygon2D
        {
            Polygon = Star(10f, 2.2f),
            Color = SandLight,
            Visible = false
        };
        _sandRoot.AddChildSafely(body);
        _sandRoot.AddChildSafely(flick);
        var spread = new Vector2((float)_random.NextDouble() - 0.5f, (float)_random.NextDouble() - 0.5f);
        return new Grain
        {
            Body = body,
            Flick = flick,
            Kind = kind,
            Target = target,
            Launch = launch,
            From = Root.GlobalPosition + spread * new Vector2(70f, 90f),
            Lift = new Vector2(spread.X * 120f, -140f - (float)_random.NextDouble() * 80f)
        };
    }

    private void UpdateGrains(float t)
    {
        foreach (var grain in _grains)
        {
            if (grain.Lost || !GodotObject.IsInstanceValid(grain.Body))
                continue;

            var u = (t - grain.Launch) / Timeline.GrainFlight;
            if (u < 0f)
                continue;

            if (Aim(grain) is { } aim)
                grain.LastAim = aim;
            else if (grain.LastAim == default)
            {
                // Its display is gone before the grain ever saw it.
                grain.Lost = true;
                grain.Body.Visible = false;
                continue;
            }

            if (u < 1f)
            {
                var e = 1f - (1f - u) * (1f - u);
                var mid = (grain.From + grain.LastAim) * 0.5f + grain.Lift;
                var a = grain.From.Lerp(mid, e);
                var b = mid.Lerp(grain.LastAim, e);
                grain.Body.Visible = true;
                grain.Body.GlobalPosition = a.Lerp(b, e);
                grain.Body.Rotation = u * 6f;
                continue;
            }

            grain.Body.Visible = false;
            var f = (t - grain.Launch - Timeline.GrainFlight) / Timeline.Flick;
            grain.Flick.Visible = f < 1f;
            if (f >= 1f)
                continue;
            grain.Flick.GlobalPosition = grain.LastAim;
            grain.Flick.Scale = Vector2.One * (0.6f + 0.8f * f);
            grain.Flick.Modulate = new Color(1f, 1f, 1f, 1f - f);
        }
    }

    // Where a grain lands on its display: centre for round displays, the top
    // band of a hand card.
    private static Vector2? Aim(Grain grain)
    {
        if (!GodotObject.IsInstanceValid(grain.Target) || !grain.Target.IsVisibleInTree())
            return null;
        var rect = grain.Target.GetGlobalRect();
        return grain.Kind == "hand"
            ? new Vector2(rect.GetCenter().X, rect.Position.Y + rect.Size.Y * 0.22f)
            : rect.GetCenter();
    }

    private static Vector2[] Star(float outer, float inner)
    {
        var points = new Vector2[8];
        for (var i = 0; i < 8; i++)
        {
            var r = i % 2 == 0 ? outer : inner;
            var a = i * MathF.PI / 4f - MathF.PI / 2f;
            points[i] = new Vector2(MathF.Cos(a), MathF.Sin(a)) * r;
        }
        return points;
    }

    /// <summary>
    /// Stasis is applied. The glass still breaks and its sand still lands; the
    /// session ends with the last grain.
    /// </summary>
    private void FinishAfterScatter()
    {
        if (!_bodyStarted || !IsActive())
        {
            Dispose();
            return;
        }
        _outro = true;
        if (_t + 0.001f >= _end)
            Dispose();
    }

    private void ReleaseExtras()
    {
        if (GodotObject.IsInstanceValid(Root))
            Root.TreeExiting -= ReleaseExtras;
        if (GodotObject.IsInstanceValid(_sandRoot))
            _sandRoot.QueueFreeSafely();
        if (ReferenceEquals(_current, this))
            _current = null;
    }

    private static Shader LoadShader() =>
        PreloadManager.Cache.GetAsset<Shader>(ShaderPath);

    private static void LogLoadFailure(Exception exception)
    {
        if (_loadFailureLogged)
            return;

        _loadFailureLogged = true;
        MainFile.Logger.Error($"Could not create Clow Time hourglass VFX from {ShaderPath}: {exception}");
    }
}
