using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>
/// The Clear card Transfer, drawn from its card face: lock-on reticles on Sakura
/// and each target, a bevelled bridge between them, and a red Strength core
/// pulled out of the enemy and carried along the bridge into Sakura. The session
/// owns presentation only; the card owns every PowerCmd and awaits the two
/// steal cues so each number lands on its beat.
/// </summary>
internal sealed class TransferVfx : CelVfxSession
{
    internal const string ShaderPath =
        MainFile.ResPath + "/shaders/card_vfx/transfer_lock.gdshader";
    internal static IReadOnlyList<string> AssetPaths { get; } = [ShaderPath];

    // Beats in whole stepped frames.
    private const int LockFrames = 4;
    private const int LockStaggerFrames = 1;
    private const int BridgeFrames = 3;
    private const int PullOutFrames = 3;
    private const int CarryFrames = 6;
    private const int AbsorbFrames = 6;
    private const int SealFrames = 3;
    private const float FadeDuration = 0.18f;
    private const float OutroLinger = 0.20f;

    // Keep in step with scripts/render_transfer_vfx.gd.
    internal const float MinReticleRadius = 70f;
    internal const float MaxReticleRadius = 130f;
    private const float ReticleRegionScale = 1.65f;
    private const float ReticleRegionPadding = 80f;
    private const float BridgeRegionHeight = 96f;
    private const float BridgePadExtra = 24f;
    // The core leaves the enemy's centre and first shows at the rim: this share
    // of its path is covered during the pull-out beat.
    private const float PullOutFlow = 0.12f;
    private const int VfxZIndex = 3000;

    private readonly Shader _shader;
    private readonly Vector2 _casterCenter;
    private readonly float _casterRadius;
    private readonly ShaderMaterial _casterReticle;
    private readonly List<PairVisual> _pairs;
    private double _busyUntilMsec;
    private bool _faded;

    private TransferVfx(
        Node2D root,
        NCombatRoom room,
        Shader shader,
        Vector2 casterCenter,
        float casterRadius,
        float casterOuterSign,
        IReadOnlyList<(Creature Target, Vector2 Center, float Radius)> targets)
        : base(root, room)
    {
        _shader = shader;
        _casterCenter = casterCenter;
        _casterRadius = casterRadius;
        _pairs = new List<PairVisual>(targets.Count);

        // Bridges first so every reticle draws over the band ends.
        foreach (var (target, center, radius) in targets)
            _pairs.Add(new PairVisual(target, center, radius, CreateBridge(center, radius)));
        _casterReticle = CreateReticle(casterCenter, casterRadius, casterOuterSign, "CasterReticle");
        for (var index = 0; index < _pairs.Count; index++)
        {
            var pair = _pairs[index];
            var outer = pair.Center.X >= casterCenter.X ? 1f : -1f;
            pair.Reticle = CreateReticle(pair.Center, pair.Radius, outer, $"TargetReticle{index}");
        }
    }

    protected override IEnumerable<ShaderMaterial> Materials => [];

    protected override float MaximumLifetime => 8f;

    internal static float ReticleRadius(Vector2 bodySize) =>
        Mathf.Clamp(Mathf.Min(bodySize.X * 0.5f, bodySize.Y * 0.36f), MinReticleRadius, MaxReticleRadius);

    internal static Task PlayOrResolveAsync(
        CardModel card,
        Creature caster,
        IReadOnlyList<Creature> targets,
        bool enhanced,
        Func<Cues, Task> resolveGameplay)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(caster);
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(resolveGameplay);

        return CelVfxSession.PlayOrResolveAsync(
            "Transfer",
            () => TryCreate(caster, targets),
            session => session.PlayPrelude(card, caster),
            scope => resolveGameplay(new Cues(scope)),
            session => session.FadeAndDispose(enhanced),
            session => session.Dispose());
    }

    internal sealed class Cues(CueScope<TransferVfx> scope)
    {
        /// <summary>The target's reticle tightens and the core leaves its body.</summary>
        internal Task PullOutAsync(Creature target)
        {
            ArgumentNullException.ThrowIfNull(target);
            return scope.InvokeAsync("pull out", session => session.PullOutAsync(target));
        }

        /// <summary>The core slides along the bridge and lands in Sakura.</summary>
        internal Task DeliverAsync(Creature target)
        {
            ArgumentNullException.ThrowIfNull(target);
            return scope.InvokeAsync("deliver", session => session.DeliverAsync(target));
        }
    }

    private static TransferVfx? TryCreate(Creature caster, IReadOnlyList<Creature> targets)
    {
        if (targets.Count == 0
            || !TryPrepare(
                "Transfer",
                static () => PreloadManager.Cache.GetAsset<Shader>(ShaderPath),
                out var room,
                out _,
                out var shader))
        {
            return null;
        }

        Node2D? root = null;
        try
        {
            if (CelVfxGeometry.ResolveCaster(room.GetCreatureNode(caster)) is not { } casterAnchor)
                return null;

            var resolved = new List<(Creature, Vector2, float)>(targets.Count);
            for (var index = 0; index < targets.Count; index++)
            {
                var target = targets[index];
                if (!target.IsAlive)
                    continue;

                var geometry = CelVfxGeometry.Resolve(room, target, index, Budget);
                resolved.Add((target, geometry.Center, ReticleRadius(geometry.Size)));
            }

            if (resolved.Count == 0)
                return null;

            root = new Node2D
            {
                Name = "SakuraTransferVfx",
                ZAsRelative = false,
                ZIndex = VfxZIndex,
                Modulate = Colors.White
            };
            room.CombatVfxContainer.AddChildSafely(root);
            root.GlobalPosition = Vector2.Zero;

            var casterCenter = casterAnchor.BodyCenter;
            var meanTargetX = resolved.Average(entry => entry.Item2.X);
            // The caster's open side faces away from the targets, as on the card.
            var casterOuter = meanTargetX >= casterCenter.X ? -1f : 1f;
            var session = new TransferVfx(
                root,
                room,
                shader,
                casterCenter,
                ReticleRadius(casterAnchor.BodySize),
                casterOuter,
                resolved);
            session.StartClock();
            return session;
        }
        catch (Exception exception)
        {
            MainFile.Logger.Error($"Could not create Transfer VFX: {exception}");
            root?.QueueFreeSafely();
            return null;
        }
    }

    private static CelVfxGeometry.GeometryBudget Budget => new(
        HorizontalPadding: 0f,
        VerticalPadding: 0f,
        MinWidth: 84f,
        MinHeight: 98f,
        MaxWidth: 300f,
        MaxHeight: 380f,
        FallbackWidth: 180f,
        FallbackHeight: 240f,
        FloorClearance: 0f,
        MaxViewportWidthFraction: 0.24f,
        MaxViewportHeightFraction: 0.44f);

    private ShaderMaterial CreateMaterial(ColorRect rect, Vector2 size)
    {
        var material = new ShaderMaterial { Shader = _shader };
        material.SetShaderParameter("region_size", size);
        material.SetShaderParameter("opacity", 1f);
        material.SetShaderParameter("lock", 0f);
        material.SetShaderParameter("squeeze", 0f);
        material.SetShaderParameter("absorb", 0f);
        material.SetShaderParameter("seal", 0f);
        material.SetShaderParameter("extend", 0f);
        material.SetShaderParameter("flow", 0f);
        material.SetShaderParameter("core_on", 0f);
        rect.Material = material;
        return material;
    }

    private ShaderMaterial CreateReticle(Vector2 center, float radius, float outerSign, string name)
    {
        var size = Vector2.One * (radius * 2f * ReticleRegionScale + ReticleRegionPadding);
        var rect = new ColorRect
        {
            Name = name,
            Color = Colors.White,
            Size = size,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        var material = CreateMaterial(rect, size);
        material.SetShaderParameter("mode", 0f);
        material.SetShaderParameter("radius", radius);
        material.SetShaderParameter("outer_sign", outerSign);
        Root.AddChild(rect);
        rect.GlobalPosition = center - size * 0.5f;
        return material;
    }

    private ShaderMaterial CreateBridge(Vector2 targetCenter, float targetRadius)
    {
        var offset = targetCenter - _casterCenter;
        var direction = offset.LengthSquared() > 1f ? offset.Normalized() : Vector2.Right;
        var start = _casterCenter + direction * _casterRadius;
        var length = Mathf.Max(offset.Length() - _casterRadius - targetRadius, 1f);
        // The pad reaches each body's centre, so the core can leave the enemy
        // and land in Sakura inside this one rect.
        var pad = Mathf.Max(_casterRadius, targetRadius) + BridgePadExtra;
        var size = new Vector2(length + pad * 2f, BridgeRegionHeight);
        var rect = new ColorRect
        {
            Name = "Bridge",
            Color = Colors.White,
            Size = size,
            PivotOffset = new Vector2(pad, size.Y * 0.5f),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        var material = CreateMaterial(rect, size);
        material.SetShaderParameter("mode", 1f);
        material.SetShaderParameter("bridge_length", length);
        material.SetShaderParameter("bridge_pad", pad);
        material.SetShaderParameter("core_start", length + targetRadius);
        material.SetShaderParameter("core_end", -_casterRadius);
        Root.AddChild(rect);
        rect.GlobalPosition = start - rect.PivotOffset;
        rect.Rotation = direction.Angle();
        return material;
    }

    private async Task<bool> PlayPrelude(CardModel card, Creature caster)
    {
        if (!await PlayCelPrelude(card, caster))
            return false;

        var tween = Track(Root.CreateTween().SetParallel());
        Beat(tween, _casterReticle, "lock", 0f, 1f, LockFrames, 0);
        for (var index = 0; index < _pairs.Count; index++)
        {
            var pair = _pairs[index];
            var lockDelay = (index + 1) * LockStaggerFrames;
            Beat(tween, pair.Reticle!, "lock", 0f, 1f, LockFrames, lockDelay);
            Beat(tween, pair.Bridge, "extend", 0f, 1f, BridgeFrames, lockDelay + 2);
        }

        var lastLock = _pairs.Count * LockStaggerFrames;
        var frames = Math.Max(LockFrames + lastLock, lastLock + 2 + BridgeFrames);
        return await WaitActive(frames / StepFrequency);
    }

    private async Task PullOutAsync(Creature target)
    {
        if (!IsActive() || Find(target) is not { Stage: PairStage.Locked } pair)
            return;

        pair.Stage = PairStage.PulledOut;
        pair.Bridge.SetShaderParameter("core_on", 1f);
        var tween = Track(Root.CreateTween().SetParallel());
        Beat(tween, pair.Reticle!, "squeeze", 0f, 1f, PullOutFrames, 0);
        Beat(tween, pair.Bridge, "flow", 0f, PullOutFlow, PullOutFrames, 0);
        MarkBusy(PullOutFrames);
        await WaitActive(PullOutFrames / StepFrequency);
    }

    private async Task DeliverAsync(Creature target)
    {
        if (!IsActive() || Find(target) is not { Stage: PairStage.PulledOut } pair)
            return;

        pair.Stage = PairStage.Delivered;
        var carry = Track(Root.CreateTween().SetParallel());
        Beat(carry, pair.Reticle!, "squeeze", 1f, 0f, CarryFrames, 0);
        Beat(carry, pair.Bridge, "flow", PullOutFlow, 1f, CarryFrames, 0,
            Tween.TransitionType.Sine, Tween.EaseType.InOut);
        MarkBusy(CarryFrames + AbsorbFrames);
        if (!await WaitActive(CarryFrames / StepFrequency))
            return;

        // Arrival: the core vanishes into Sakura, whose reticle clenches and
        // throws the Strength and Dexterity arcs.
        pair.Bridge.SetShaderParameter("core_on", 0f);
        var absorb = Track(Root.CreateTween().SetParallel());
        Beat(absorb, _casterReticle, "squeeze", 1f, 0f, AbsorbFrames, 0);
        Beat(absorb, _casterReticle, "absorb", 0f, 1f, AbsorbFrames, 0);
    }

    private void FadeAndDispose(bool enhanced)
    {
        if (_faded || !IsActive())
        {
            Dispose();
            return;
        }

        _faded = true;
        TaskHelper.RunSafely(PlayOutro(enhanced));
    }

    private async Task PlayOutro(bool enhanced)
    {
        if (enhanced)
        {
            var seal = Track(Root.CreateTween());
            Beat(seal, _casterReticle, "seal", 0f, 1f, SealFrames, 0);
            MarkBusy(SealFrames + 4);
        }

        var remaining = (float)Math.Max(0d, (_busyUntilMsec - Godot.Time.GetTicksMsec()) / 1000d);
        if (!await WaitActive(remaining + OutroLinger))
            return;

        var fade = Track(Root.CreateTween());
        fade.TweenProperty(Root, "modulate:a", 0f, FadeDuration);
        fade.TweenCallback(Callable.From(Dispose));
    }

    private void MarkBusy(int frames)
    {
        var until = Godot.Time.GetTicksMsec() + frames / StepFrequency * 1000d;
        _busyUntilMsec = Math.Max(_busyUntilMsec, until);
    }

    private PairVisual? Find(Creature target) =>
        _pairs.FirstOrDefault(candidate => ReferenceEquals(candidate.Target, target));

    /// <summary>
    /// Drives one uniform from <paramref name="from"/> to <paramref name="to"/>
    /// over a whole number of stepped frames. The motion itself is continuous:
    /// quantizing a large travel (the core crosses the bridge in a few frames)
    /// onto the 12 Hz clock makes it jump tens of pixels per step, which reads
    /// as the game dropping frames. Explicit ends keep the tween from reading
    /// the material's current value.
    /// </summary>
    private static void Beat(
        Tween tween,
        ShaderMaterial material,
        string name,
        float from,
        float to,
        int frames,
        int delayFrames,
        Tween.TransitionType transition = Tween.TransitionType.Cubic,
        Tween.EaseType ease = Tween.EaseType.Out)
    {
        tween.TweenMethod(
                Callable.From<float>(value => material.SetShaderParameter(name, value)),
                from,
                to,
                Math.Max(frames, 1) / StepFrequency)
            .SetDelay(delayFrames / StepFrequency)
            .SetTrans(transition)
            .SetEase(ease);
    }

    private enum PairStage
    {
        Locked,
        PulledOut,
        Delivered
    }

    private sealed class PairVisual(Creature target, Vector2 center, float radius, ShaderMaterial bridge)
    {
        internal Creature Target { get; } = target;
        internal Vector2 Center { get; } = center;
        internal float Radius { get; } = radius;
        internal ShaderMaterial Bridge { get; } = bridge;
        internal ShaderMaterial? Reticle { get; set; }
        internal PairStage Stage { get; set; } = PairStage.Locked;
    }
}
