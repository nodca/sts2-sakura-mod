using System.Threading;
using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>
/// Clear Card Spiral (螺旋): a continuous glass staircase corridor with deep-navy
/// beads unfurls behind and in front of the target enemy, cinches on impact with
/// the card-face eight-point ice starburst and curved spiral slash, then flings
/// one (or with the Extra effect, twin interwoven) helical return ribbon(s) into
/// the local Draw Pile with a one-shot miniature spiral ripple.
/// <para>
/// <c>scripts/render_spiral_vfx.gd</c> is the source of truth for beat and scaling
/// constants; keep both in step.
/// </para>
/// </summary>
internal sealed class SpiralCorridorVfx : CelVfxSession
{
    internal const string ShaderPath =
        MainFile.ResPath + "/shaders/card_vfx/spiral_corridor.gdshader";

    internal static IReadOnlyList<string> AssetPaths { get; } = [ShaderPath];

    // Keep in step with scripts/render_spiral_vfx.gd (seconds after the shared prelude).
    internal const float UnfurlDuration = 0.16f;
    internal const float CollapseDuration = 0.28f;
    internal const float BurstDuration = 0.30f;
    internal const float RibbonFlightDuration = 0.36f;
    internal const float RibbonTailClear = 0.14f;
    internal const float PileRippleLead = 0.03f;
    internal const float PileRippleDuration = 0.30f;

    internal const float SquashY = 0.42f;
    internal const float RibbonWidth = 22f;
    private const float RippleRegionSize = 260f;
    private const int OverlayZIndex = 3000;
    private const int RibbonSegments = 64;

    private static readonly float[] CutAngles = [-0.34f, 0.30f, -0.56f, 0.48f];
    private static int _playSequence;

    private readonly Shader _shader;
    private readonly Node2D _backRoot;
    private readonly Node2D? _overlay;
    private readonly ShaderMaterial _backCorridor;
    private readonly ShaderMaterial _frontCorridor;
    private readonly ShaderMaterial _impactBurst;
    private readonly List<ShaderMaterial> _ribbons = [];
    private ShaderMaterial? _pileRipple;
    private readonly Vector2 _targetCenter;
    private readonly float _memoryTier;
    private readonly float _stepCount;
    private readonly float _spiralTurns;
    private readonly float _baseSpin;
    private readonly bool _local;

    private float _t;
    private float _unfurlStart = -1f;
    private float _impactAt = -1f;
    private float _ribbonStart = -1f;
    private float _rippleStart = -1f;
    private bool _outroRequested;
    private bool _auxReleased;

    private SpiralCorridorVfx(
        Node2D root,
        Node2D backRoot,
        Node2D? overlay,
        NCombatRoom room,
        Shader shader,
        CelVfxGeometry.TargetGeometry geometry,
        int memoryCount,
        int sequence,
        bool local)
        : base(root, room)
    {
        _shader = shader;
        _backRoot = backRoot;
        _overlay = overlay;
        _targetCenter = geometry.Center;
        _memoryTier = MemoryTier(memoryCount);
        _stepCount = StepCountForMemory(memoryCount);
        _spiralTurns = SpiralTurnsForMemory(memoryCount);
        _baseSpin = 0.35f + (sequence % 5) * 1.18f;
        _local = local;

        root.GlobalPosition = Vector2.Zero;
        root.Scale = Vector2.One;
        backRoot.GlobalPosition = Vector2.Zero;
        backRoot.Scale = Vector2.One;

        var radiusX = Math.Clamp(geometry.Size.X * 0.80f, 128f, 224f);
        var halfSpanY = Math.Clamp(geometry.Size.Y * 0.44f, 108f, 196f);
        var corridorRadii = new Vector2(radiusX, halfSpanY);
        var corridorRegion = new Vector2(
            Math.Max(520f, radiusX * 3.2f),
            Math.Max(520f, (halfSpanY + radiusX * SquashY) * 2.55f));
        var burstRegion = Vector2.One * Math.Clamp(Math.Max(geometry.Size.X, geometry.Size.Y) * 1.65f, 460f, 680f);
        var cutAngle = CutAngles[Math.Abs(sequence) % CutAngles.Length];
        var seed = 0.37f + (sequence % 7) * 0.29f;

        _backCorridor = AddCentredRect(backRoot, "BackCorridor", _targetCenter, corridorRegion, 0f, corridorRadii, cutAngle, seed);
        _frontCorridor = AddCentredRect(root, "FrontCorridor", _targetCenter, corridorRegion, 1f, corridorRadii, cutAngle, seed);
        _impactBurst = AddCentredRect(root, "ImpactBurst", _targetCenter, burstRegion, 2f, corridorRadii, cutAngle, seed);

        CombatManager.Instance.CombatEnded += OnSpiralCombatEnded;
        root.TreeExiting += ReleaseAuxNodes;
        backRoot.TreeExiting += ReleaseAuxNodes;
        if (overlay is not null)
            overlay.TreeExiting += ReleaseAuxNodes;
    }

    private static CelVfxGeometry.GeometryBudget Budget => new(
        HorizontalPadding: 0f,
        VerticalPadding: 0f,
        MinWidth: 96f,
        MinHeight: 110f,
        MaxWidth: 360f,
        MaxHeight: 460f,
        FallbackWidth: 180f,
        FallbackHeight: 280f,
        FloorClearance: 0f,
        MaxViewportWidthFraction: 0.26f,
        MaxViewportHeightFraction: 0.52f);

    protected override IEnumerable<ShaderMaterial> Materials => [];

    protected override float MaximumLifetime => 8f;

    internal static float MemoryTier(int memoryCount) =>
        Math.Clamp(Math.Max(0, memoryCount) / 8f, 0f, 1f);

    internal static float StepCountForMemory(int memoryCount) =>
        MathF.Round(Mathf.Lerp(14f, 22f, MemoryTier(memoryCount)));

    internal static float SpiralTurnsForMemory(int memoryCount) =>
        Mathf.Lerp(1.42f, 2.02f, MemoryTier(memoryCount));

    internal static Task PlayOrResolveAsync(
        CardModel card,
        Creature caster,
        Creature target,
        int memoryCount,
        Func<Cues, Task> resolveGameplay)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(caster);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(resolveGameplay);

        return CelVfxSession.PlayOrResolveAsync(
            "Spiral corridor",
            () => TryCreate(caster, target, memoryCount),
            session => session.PlayPrelude(card, caster),
            scope => resolveGameplay(new Cues(scope)),
            session => session.BeginOutro(),
            session => session.DisposePresentation());
    }

    internal sealed class Cues(CueScope<SpiralCorridorVfx> scope)
    {
        /// <summary>
        /// Keeps native slash feedback whenever the corridor is not drawing
        /// (VFX disabled, TestMode, or load failure).
        /// </summary>
        internal string? FallbackHitVfx => scope.IsLive ? null : SakuraNativeHitFx.Slash;

        /// <summary>Cinches the corridor and fires the starburst &amp; spiral slash right before damage.</summary>
        internal void Impact() =>
            scope.Invoke("impact", static session => session.Impact());

        /// <summary>Flings 1 (or 2 with Extra) helical ribbon(s) into the local Draw Pile.</summary>
        internal void ReturnToDrawPile(bool isExtra) =>
            scope.Invoke("return to draw pile", session => session.ReturnToDrawPile(isExtra));
    }

    private static SpiralCorridorVfx? TryCreate(Creature caster, Creature target, int memoryCount)
    {
        if (!TryPrepare(
                "Spiral corridor",
                static () => PreloadManager.Cache.GetAsset<Shader>(ShaderPath),
                out var room,
                out _,
                out var shader))
        {
            return null;
        }

        Node2D? root = null;
        Node2D? backRoot = null;
        Node2D? overlay = null;
        try
        {
            var geometry = CelVfxGeometry.Resolve(room, target, 0, Budget);
            var local = caster.Player is { } player && LocalContext.IsMe(player);

            root = new Node2D { Name = "SakuraSpiralCorridorVfx" };
            room.CombatVfxContainer.AddChildSafely(root);

            backRoot = new Node2D { Name = "SakuraSpiralCorridorBackVfx" };
            room.BackCombatVfxContainer.AddChildSafely(backRoot);

            if (local && room.Ui is { } ui)
            {
                overlay = new Node2D
                {
                    Name = "SakuraSpiralDrawPileOverlay",
                    TopLevel = true,
                    ZAsRelative = false,
                    ZIndex = OverlayZIndex
                };
                ui.AddChildSafely(overlay);
                overlay.GlobalPosition = Vector2.Zero;
            }

            var sequence = Interlocked.Increment(ref _playSequence);
            var session = new SpiralCorridorVfx(
                root,
                backRoot,
                overlay,
                room,
                shader,
                geometry,
                memoryCount,
                sequence,
                local);
            session.StartClock();
            return session;
        }
        catch (Exception exception)
        {
            MainFile.Logger.Error($"Could not create Spiral corridor VFX: {exception}");
            root?.QueueFreeSafely();
            backRoot?.QueueFreeSafely();
            overlay?.QueueFreeSafely();
            return null;
        }
    }

    private async Task<bool> PlayPrelude(CardModel card, Creature caster)
    {
        if (!await PlayCelPrelude(card, caster))
            return false;

        _unfurlStart = _t;
        UpdateFrame();
        return await WaitActive(UnfurlDuration);
    }

    private void Impact()
    {
        if (!IsActive() || _impactAt >= 0f)
            return;

        if (_unfurlStart < 0f)
            _unfurlStart = Math.Max(0f, _t - UnfurlDuration);
        _impactAt = _t;
        UpdateFrame();
    }

    private void ReturnToDrawPile(bool isExtra)
    {
        if (!IsActive() || _ribbonStart >= 0f || !_local || _overlay is null || !GodotObject.IsInstanceValid(_overlay))
            return;

        if (!PileExchangeVfx.TryGetPileCenter(Room, PileType.Draw, out var drawPile))
            return;

        _ribbonStart = _t;
        _rippleStart = _ribbonStart + RibbonFlightDuration - PileRippleLead;

        var whiteImg = Image.CreateEmpty(4, 4, false, Image.Format.Rgba8);
        whiteImg.Fill(Colors.White);
        var whiteTex = ImageTexture.CreateFromImage(whiteImg);

        var strands = isExtra ? 2 : 1;
        for (var s = 0; s < strands; s++)
        {
            var points = BuildRibbonPoints(s, isExtra, drawPile);
            var line = new Line2D
            {
                Name = $"ReturnRibbon{s}",
                Points = points,
                Width = RibbonWidth,
                Texture = whiteTex,
                TextureMode = Line2D.LineTextureMode.Stretch,
                JointMode = Line2D.LineJointMode.Round
            };
            var mat = CreateMaterial(3f, new Vector2(600f, RibbonWidth), Vector2.Zero, 0f, 0f);
            mat.SetShaderParameter("strand_tint", (float)s);
            mat.SetShaderParameter("path_length_px", PolylineLength(points));
            mat.SetShaderParameter("ribbon_width_px", RibbonWidth);
            line.Material = mat;
            _overlay.AddChild(line);
            _ribbons.Add(mat);
        }

        var rippleSize = Vector2.One * RippleRegionSize;
        var rippleRect = new ColorRect
        {
            Name = "DrawPileSpiralRipple",
            Color = Colors.White,
            Size = rippleSize,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        var rippleMat = CreateMaterial(4f, rippleSize, Vector2.Zero, 0f, 0f);
        rippleMat.SetShaderParameter("is_extra", isExtra ? 1f : 0f);
        rippleRect.Material = rippleMat;
        _overlay.AddChild(rippleRect);
        rippleRect.GlobalPosition = drawPile - rippleSize * 0.5f;
        _pileRipple = rippleMat;

        UpdateFrame();
    }

    private Vector2[] BuildRibbonPoints(int strand, bool isExtra, Vector2 drawPile)
    {
        var a = _targetCenter + new Vector2(0f, -16f);
        var d = drawPile;
        var b = new Vector2(a.X - 240f, a.Y + 210f);
        var c = new Vector2(d.X + 360f, d.Y - 260f);
        var phaseOffset = strand == 1 ? MathF.PI : 0f;
        var amp = isExtra ? 26f : 18f;

        var points = new Vector2[RibbonSegments + 1];
        for (var i = 0; i <= RibbonSegments; i++)
        {
            var u = i / (float)RibbonSegments;
            var basePt = Bezier(a, b, c, d, u);
            var nextU = MathF.Min(1f, u + 0.01f);
            var prevU = MathF.Max(0f, u - 0.01f);
            var tangent = (Bezier(a, b, c, d, nextU) - Bezier(a, b, c, d, prevU)).Normalized();
            var normal = new Vector2(-tangent.Y, tangent.X);
            var wave = MathF.Sin(u * MathF.Tau * 2.5f + phaseOffset) * MathF.Sin(u * MathF.PI) * amp;
            var globalPt = basePt + normal * wave;
            points[i] = _overlay is not null ? _overlay.ToLocal(globalPt) : globalPt;
        }
        return points;
    }

    private void BeginOutro()
    {
        if (!IsActive())
        {
            DisposePresentation();
            return;
        }

        if (_impactAt < 0f)
            _impactAt = _t;
        _outroRequested = true;
        if (_t >= EndTime())
            DisposePresentation();
    }

    protected override void OnFrame(float delta)
    {
        _t += delta;
        UpdateFrame();
        if (_outroRequested && _t >= EndTime())
            DisposePresentation();
    }

    private float EndTime()
    {
        var end = _impactAt >= 0f
            ? _impactAt + MathF.Max(CollapseDuration, BurstDuration)
            : (_unfurlStart >= 0f ? _unfurlStart + UnfurlDuration : _t);

        if (_ribbonStart >= 0f)
            end = MathF.Max(end, _ribbonStart + RibbonFlightDuration + RibbonTailClear);
        if (_rippleStart >= 0f)
            end = MathF.Max(end, _rippleStart + PileRippleDuration);

        return end + 0.04f;
    }

    private void UpdateFrame()
    {
        if (_unfurlStart >= 0f)
        {
            var localT = MathF.Max(0f, _t - _unfurlStart);
            var openU = EaseOutCubic(Math.Clamp(localT / UnfurlDuration, 0f, 1f));
            var collapseU = _impactAt >= 0f
                ? Math.Clamp((_t - _impactAt) / CollapseDuration, 0f, 1f)
                : 0f;
            var spin = _baseSpin + openU * 1.40f + EaseInCubic(collapseU) * 2.85f;
            var flash = _impactAt >= 0f && _t <= _impactAt + 0.16f
                ? 1f - Math.Clamp(MathF.Abs(_t - _impactAt) / 0.14f, 0f, 1f)
                : 0f;
            var corridorOpacity = _impactAt >= 0f && _t >= _impactAt + CollapseDuration ? 0f : 1f;

            ApplyCorridor(_backCorridor, openU, collapseU, flash, spin, corridorOpacity);
            ApplyCorridor(_frontCorridor, openU, collapseU, flash, spin, corridorOpacity);
        }

        if (_impactAt >= 0f)
        {
            var burstLocal = _t - _impactAt;
            var burstU = Math.Clamp(burstLocal / BurstDuration, 0f, 1f);
            var burstOpacity = burstLocal >= 0f && burstLocal < BurstDuration ? 1f : 0f;
            _impactBurst.SetShaderParameter("burst_progress", burstU);
            _impactBurst.SetShaderParameter("opacity", burstOpacity);
        }

        if (_ribbonStart >= 0f && _ribbons.Count > 0)
        {
            var rLocal = _t - _ribbonStart;
            var head = EaseOutCubic(Math.Clamp(rLocal / RibbonFlightDuration, 0f, 1f));
            var tail = EaseInCubic(Math.Clamp(
                (rLocal - 0.09f) / (RibbonFlightDuration + RibbonTailClear - 0.09f),
                0f,
                1f));
            var rOpacity = rLocal > 0f && tail < 0.995f ? 1f : 0f;
            for (var i = 0; i < _ribbons.Count; i++)
            {
                var mat = _ribbons[i];
                mat.SetShaderParameter("head", head);
                mat.SetShaderParameter("tail", tail);
                mat.SetShaderParameter("phase", _t * 6f + i * 0.5f);
                mat.SetShaderParameter("opacity", rOpacity);
            }
        }

        if (_rippleStart >= 0f && _pileRipple is not null)
        {
            var ripLocal = _t - _rippleStart;
            var ripU = Math.Clamp(ripLocal / PileRippleDuration, 0f, 1f);
            var ripOpacity = ripLocal >= 0f && ripLocal < PileRippleDuration ? 1f : 0f;
            _pileRipple.SetShaderParameter("ripple_progress", ripU);
            _pileRipple.SetShaderParameter("opacity", ripOpacity);
        }
    }

    private void ApplyCorridor(
        ShaderMaterial mat,
        float openU,
        float collapseU,
        float flash,
        float spin,
        float opacity)
    {
        mat.SetShaderParameter("open_progress", openU);
        mat.SetShaderParameter("collapse_progress", collapseU);
        mat.SetShaderParameter("impact_flash", flash);
        mat.SetShaderParameter("spin_angle", spin);
        mat.SetShaderParameter("opacity", opacity);
    }

    private ShaderMaterial AddCentredRect(
        Node2D parent,
        string name,
        Vector2 centre,
        Vector2 size,
        float mode,
        Vector2 corridorRadii,
        float cutAngle,
        float seed)
    {
        var rect = new ColorRect
        {
            Name = name,
            Color = Colors.White,
            Size = size,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        var mat = CreateMaterial(mode, size, corridorRadii, cutAngle, seed);
        rect.Material = mat;
        parent.AddChild(rect);
        rect.GlobalPosition = centre - size * 0.5f;
        return mat;
    }

    // Writes every shader uniform explicitly at creation (Nil-uniform rule).
    private ShaderMaterial CreateMaterial(
        float mode,
        Vector2 regionSize,
        Vector2 corridorRadii,
        float cutAngle,
        float seed)
    {
        var mat = new ShaderMaterial { Shader = _shader };
        mat.SetShaderParameter("mode", mode);
        mat.SetShaderParameter("region_size", regionSize);
        mat.SetShaderParameter("opacity", 0f);
        mat.SetShaderParameter("open_progress", 0f);
        mat.SetShaderParameter("collapse_progress", 0f);
        mat.SetShaderParameter("impact_flash", 0f);
        mat.SetShaderParameter("spin_angle", _baseSpin);
        mat.SetShaderParameter("memory_tier", _memoryTier);
        mat.SetShaderParameter("step_count", _stepCount);
        mat.SetShaderParameter("spiral_turns", _spiralTurns);
        mat.SetShaderParameter("corridor_radii", corridorRadii);
        mat.SetShaderParameter("squash_y", SquashY);
        mat.SetShaderParameter("seed", seed);
        mat.SetShaderParameter("burst_progress", 0f);
        mat.SetShaderParameter("cut_angle", cutAngle);
        mat.SetShaderParameter("head", 0f);
        mat.SetShaderParameter("tail", 0f);
        mat.SetShaderParameter("phase", 0f);
        mat.SetShaderParameter("strand_tint", 0f);
        mat.SetShaderParameter("path_length_px", 1480f);
        mat.SetShaderParameter("ribbon_width_px", RibbonWidth);
        mat.SetShaderParameter("ripple_progress", 0f);
        mat.SetShaderParameter("is_extra", 0f);
        return mat;
    }

    private void OnSpiralCombatEnded(CombatRoom _) => ReleaseAuxNodes();

    private void ReleaseAuxNodes()
    {
        if (_auxReleased)
            return;
        _auxReleased = true;

        CombatManager.Instance.CombatEnded -= OnSpiralCombatEnded;
        if (GodotObject.IsInstanceValid(Root))
            Root.TreeExiting -= ReleaseAuxNodes;
        if (GodotObject.IsInstanceValid(_backRoot))
        {
            _backRoot.TreeExiting -= ReleaseAuxNodes;
            _backRoot.QueueFreeSafely();
        }
        if (_overlay is not null && GodotObject.IsInstanceValid(_overlay))
        {
            _overlay.TreeExiting -= ReleaseAuxNodes;
            _overlay.QueueFreeSafely();
        }
    }

    private void DisposePresentation()
    {
        ReleaseAuxNodes();
        Dispose();
    }

    private static float EaseOutCubic(float t)
    {
        var inv = 1f - t;
        return 1f - inv * inv * inv;
    }

    private static float EaseInCubic(float t) => t * t * t;

    private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
    {
        var omt = 1f - t;
        return omt * omt * omt * a
            + 3f * omt * omt * t * b
            + 3f * omt * t * t * c
            + t * t * t * d;
    }

    private static float PolylineLength(Vector2[] points)
    {
        var total = 0f;
        for (var i = 1; i < points.Length; i++)
            total += points[i - 1].DistanceTo(points[i]);
        return MathF.Max(total, 1f);
    }
}
