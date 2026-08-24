using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>
/// How much presentation a Freeze play gets, chosen by how often that play
/// happens. The uncommon Clow card starts one tier up from a Basic attack, and
/// the activated and Sakura paths share the richest tier — both are "pay more,
/// get a bigger show", and the difference between them is handed to target
/// count and gameplay numbers, not to a second orchestration.
/// </summary>
/// <remarks>
/// One intensity axis on the Sword ruling: a single <c>weight</c> uniform buys
/// width, brightness, and growth frames together. Light stays reserved for a
/// future Basic ice card that would be played far more often.
/// </remarks>
internal enum FreezeWeight
{
    /// <summary><c>ClowFreeze</c> played plainly.</summary>
    Medium,

    /// <summary><c>ClowFreeze</c>'s activated path and <c>SakuraFreeze</c>.</summary>
    Heavy
}

/// <summary>
/// Freeze's ice prison: one crystal cage per target, grown from the ground,
/// held frozen for two stepped frames, then shattered target by target so each
/// burst lands on the frame its own damage number does.
/// </summary>
/// <remarks>
/// A single class deriving from <see cref="CelVfxSession"/> rather than Aqua's
/// outer-static-plus-nested-session pair: <c>TryPrepare</c> is protected, so an
/// outer static class cannot reach it without restating the guard logic.
/// </remarks>
internal sealed class FreezeCageVfx : CelVfxSession
{
    internal const string ScenePath =
        MainFile.ResPath + "/scenes/combat/card_vfx/freeze_cage_vfx.tscn";
    internal const string TargetScenePath =
        MainFile.ResPath + "/scenes/combat/card_vfx/freeze_cage_target.tscn";
    internal const string ShaderPath =
        MainFile.ResPath + "/shaders/card_vfx/freeze_cage.gdshader";
    internal static IReadOnlyList<string> AssetPaths { get; } = [ScenePath, TargetScenePath];

    /// <summary>
    /// Through can hand one play every enemy on the line; five is the room's
    /// realistic worst case and sizes the lifetime cap and the envelope
    /// assertion.
    /// </summary>
    internal const int WorstCaseTargets = 5;

    // Beats, in whole frames of the shared stepped clock. The tall rear spines
    // need a few frames to read as individual ground-laid crystals; Heavy buys
    // one additional frame with its extra structures.
    internal const int MediumGrowthSteps = 5;
    internal const int HeavyGrowthSteps = 6;

    /// <summary>
    /// A hold lasts this long, matching <see cref="CelVfxSession.BeginHold"/> at
    /// two stepped frames. The hold is awaited inside the prelude so the frozen
    /// field is actually shown before gameplay can land: this card's signature
    /// is the whole field stopping, not the damage arriving during one.
    /// </summary>
    internal const int HoldSteps = 2;
    internal const float HoldDuration = HoldSteps / StepFrequency;

    // The block beat: a two-frame cold flash on the enemy-side ground ring,
    // answering GainBlock without building a caster-side element the shield
    // already owns.
    internal const int GlintSteps = 2;
    internal const float GlintDuration = GlintSteps / StepFrequency;

    // The scatter burst, paced in whole frames. Terminal velocity: the tween is
    // linear, so the shard front covers equal ground every frame.
    internal const int ShatterBurstSteps = 4;
    internal const float ShatterBurstDuration = ShatterBurstSteps / StepFrequency;

    // The grounded stumps outlast the burst by this much before the outro's
    // sublimation finishes them; sized with the outro to the design's ~0.67 s
    // residue read.
    internal const int StumpSublimeSteps = 8;
    internal const float StumpSublimeDuration = StumpSublimeSteps / StepFrequency;

    internal const float FadeDuration = 0.38f;

    /// <summary>
    /// Wall-clock bound on the shared prelude before the cages start growing:
    /// the chibi route is card rise 0.24 s plus wand tap 0.08 s, plus margin.
    /// </summary>
    private const float WorstPreludeSeconds = 0.40f;

    private const float SublimateLiftPx = 12f;

    private static bool _loadFailureLogged;

    private readonly Dictionary<Creature, CageVisual> _cages = [];
    private readonly Node2D _backRoot;
    private readonly FreezeWeight _weight;
    private bool _faded;

    private FreezeCageVfx(
        Node2D root,
        Node2D backRoot,
        NCombatRoom room,
        PackedScene targetScene,
        IReadOnlyList<Creature> creatures,
        FreezeWeight weight)
        : base(root, room)
    {
        _backRoot = backRoot ?? throw new ArgumentNullException(nameof(backRoot));
        _weight = weight;
        var frontCages = root.GetNode<Node2D>("%Cages");
        var backCages = backRoot.GetNode<Node2D>("%Cages");

        for (var index = 0; index < creatures.Count; index++)
        {
            var creature = creatures[index];
            if (_cages.ContainsKey(creature))
                continue;
            var geometry = CelVfxGeometry.Resolve(room, creature, index, Budget);
            _cages.Add(creature, new CageVisual(
                targetScene,
                backCages,
                frontCages,
                geometry,
                index,
                weight));
        }

        // Subscribe only after every target layer exists. If a target scene or
        // geometry lookup fails during construction, this session contributes
        // no extra callbacks to the room's event sources.
        CombatManager.Instance.CombatEnded += OnFreezeCombatEnded;
        root.TreeExiting += OnRootTreeExiting;
    }

    /// <summary>
    /// The cage is slightly wider and taller than the enemy it imprisons: the
    /// padding gives the front pane clearance over the silhouette, and the
    /// region's height leaves headroom above the shoulder line for the jagged
    /// crown the shader draws there.
    /// </summary>
    private static CelVfxGeometry.GeometryBudget Budget => new(
        HorizontalPadding: 16f,
        VerticalPadding: 22f,
        MinWidth: 200f,
        MinHeight: 240f,
        MaxWidth: 380f,
        MaxHeight: 420f,
        FallbackWidth: 220f,
        FallbackHeight: 280f,
        FloorClearance: 6f,
        MaxViewportWidthFraction: 0.28f,
        MaxViewportHeightFraction: 0.52f);

    protected override IEnumerable<ShaderMaterial> Materials =>
        _cages.Values.SelectMany(static cage => cage.Materials);

    /// <summary>
    /// Safety net on wall clock, not a timer. The compressed worst case is the
    /// prelude, growth, the hold, five serialized target bursts with their
    /// gameplay awaits pacing them, and the residue sublimation — about 3.5 s
    /// of field. Sized well clear of that envelope, because a cap set tight
    /// becomes a truncation bug.
    /// </summary>
    protected override float MaximumLifetime => 9f;

    /// <summary>Growth span, counted in whole frames of the stepped clock.</summary>
    internal static int GrowthSteps(FreezeWeight weight) =>
        weight == FreezeWeight.Medium ? MediumGrowthSteps : HeavyGrowthSteps;

    internal static float GrowthSeconds(FreezeWeight weight) =>
        GrowthSteps(weight) / StepFrequency;

    /// <summary>
    /// The intensity axis as the shader consumes it. One number drives cage
    /// width, brightness, and growth frames, which is what keeps the tiers a
    /// parameter rather than two implementations.
    /// </summary>
    internal static float WeightValue(FreezeWeight weight) => weight switch
    {
        FreezeWeight.Medium => 0.5f,
        _ => 1f
    };

    /// <summary>
    /// The whole field's wall-clock envelope for a given target count: the
    /// shared prelude, the longest tier's growth, the hold, the one glint beat,
    /// every target's serialized burst, and the residue sublimation through the
    /// outro. The contract test asserts the total stays bounded as targets
    /// accumulate and clears the lifetime cap.
    /// </summary>
    internal static float TotalEnvelopeSeconds(int targetCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(targetCount);
        return WorstPreludeSeconds
            + GrowthSeconds(FreezeWeight.Heavy)
            + HoldDuration
            + GlintDuration
            + targetCount * ShatterBurstDuration
            + StumpSublimeDuration
            + FadeDuration;
    }

    internal static Task PlayOrResolveAsync(
        CardModel card,
        Creature? caster,
        IReadOnlyList<Creature> targets,
        FreezeWeight weight,
        Func<Cues, Task> resolveGameplay)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(resolveGameplay);

        return CelVfxSession.PlayOrResolveAsync(
            "Freeze cage",
            () => TryCreate(targets, weight),
            session => session.PlayPrelude(card, caster),
            scope => resolveGameplay(new Cues(scope)),
            session => session.FadeAndDispose(),
            session => session.DisposePresentation());
    }

    internal sealed class Cues(CueScope<FreezeCageVfx> scope)
    {
        internal void Shatter(Creature target)
        {
            ArgumentNullException.ThrowIfNull(target);
            scope.Invoke("shatter", session => session.Shatter(target));
        }
    }

    private static FreezeCageVfx? TryCreate(IReadOnlyList<Creature> targets, FreezeWeight weight)
    {
        ArgumentNullException.ThrowIfNull(targets);
        if (targets.Count == 0)
            return null;
        if (!TryPrepare(
                "Freeze cage",
                LoadScenes,
                out var room,
                out _,
                out var scenes))
        {
            return null;
        }

        Node2D? root = null;
        Node2D? backRoot = null;
        try
        {
            root = scenes.Root.Instantiate<Node2D>();
            root.Name = "SakuraFreezeCageVfx";
            root.ZAsRelative = true;
            root.ZIndex = 0;
            room.CombatVfxContainer.AddChildSafely(root);

            backRoot = scenes.Root.Instantiate<Node2D>();
            backRoot.Name = "SakuraFreezeCageBackVfx";
            backRoot.ZAsRelative = true;
            backRoot.ZIndex = 0;
            room.BackCombatVfxContainer.AddChildSafely(backRoot);

            var session = new FreezeCageVfx(root, backRoot, room, scenes.Target, targets, weight);
            // Started after construction, never inside it: the base clock pulls
            // Materials, and during a base constructor the subclass field backing
            // it is still empty.
            session.StartClock();
            return session;
        }
        catch (Exception exception)
        {
            LogLoadFailure(exception);
            root?.QueueFreeSafely();
            backRoot?.QueueFreeSafely();
            return null;
        }
    }

    /// <summary>
    /// Shared wand tap, magic circle, and speed lines, then every cage grows
    /// from the ground in parallel over whole stepped frames. The prelude then
    /// holds the whole field for two stepped frames and waits that hold out:
    /// the freeze has to be shown before gameplay, because the card's reading
    /// is a stopped world that damage then breaks.
    /// </summary>
    /// <remarks>
    /// Nothing moves during the hold — growth has completed and the glint and
    /// shatter tweens do not exist yet — so <see cref="CelVfxSession.BeginHold"/>
    /// freezing shader time freezes everything, and no tween has to be delayed
    /// past it.
    /// </remarks>
    private async Task<bool> PlayPrelude(CardModel card, Creature? caster)
    {
        if (!await PlayCelPrelude(card, caster))
            return false;

        var steps = GrowthSteps(_weight);
        var duration = GrowthSeconds(_weight);
        var growth = Track(Root.CreateTween().SetParallel());
        foreach (var cage in _cages.Values)
        {
            var materials = cage.Materials;
            // The floor keeps the front on whole growth frames; the shader
            // re-snaps the same value onto the stepped clock, as Snow's curtain
            // does. Both layers consume the same session-owned step count.
            growth.TweenMethod(
                Callable.From<float>(value =>
                {
                    var rise = Mathf.Floor(Mathf.Clamp(value, 0f, 1f) * steps) / steps;
                    foreach (var material in materials)
                        material.SetShaderParameter("rise", rise);
                }),
                0f,
                1f,
                duration);
        }
        if (!await WaitActive(duration))
            return false;

        BeginHold(HoldSteps);
        if (!await WaitActive(HoldDuration))
            return false;

        // Gameplay enters here: GainBlock is the callback's first action, and
        // the glint is its two-frame answer on every cage's ground ring.
        BeginGlintBeat();
        return IsActive();
    }

    /// <summary>
    /// The block beat: the glint uniform rises for one stepped frame and falls
    /// for one, so the ground ring flashes rather than ramps. A fresh tracked
    /// tween, created only after the hold has been waited out.
    /// </summary>
    private void BeginGlintBeat()
    {
        var oneStep = 1f / StepFrequency;
        var glint = Track(Root.CreateTween().SetParallel());
        foreach (var cage in _cages.Values)
        {
            var material = cage.FrontMaterial;
            glint.TweenMethod(
                Callable.From<float>(value => material.SetShaderParameter("glint", value)),
                0f,
                1f,
                oneStep);
            glint.TweenMethod(
                    Callable.From<float>(value => material.SetShaderParameter("glint", value)),
                    1f,
                    0f,
                    oneStep)
                .SetDelay(oneStep);
        }
    }

    /// <summary>
    /// One target's cage bursts apart on the frame its damage lands. The
    /// scatter is the shader's: one <c>shatter</c> progress thrown radially at
    /// terminal velocity, so the session owns no per-shard motion at all.
    /// Idempotent per target; a target that died to an earlier through hit
    /// keeps its cage whole for the outro instead of shattering on a corpse.
    /// </summary>
    private void Shatter(Creature target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (!IsActive() || !target.IsAlive
            || !_cages.TryGetValue(target, out var cage)
            || cage.HasShattered)
            return;

        cage.HasShattered = true;
        // Linear in time, so the shard front covers equal ground every frame —
        // the same terminal-velocity language as Snow's settling fall.
        var shatter = Track(cage.FrontRoot.CreateTween().SetParallel());
        shatter.TweenMethod(
            Callable.From<float>(value =>
            {
                foreach (var material in cage.Materials)
                    material.SetShaderParameter("shatter", value);
            }),
            0f,
            1f,
            ShatterBurstDuration);
    }

    /// <summary>
    /// Sublimates the residue: opacity thins the stumps and the root lifts a
    /// few pixels, then the session disposes. This is the Release beat of the
    /// session contract; the base <c>Dispose</c> it ends in is idempotent and
    /// also covers combat end, tree exit, exceptions, and the lifetime cap.
    /// </summary>
    private void FadeAndDispose()
    {
        if (_faded || !IsActive())
        {
            DisposePresentation();
            return;
        }

        _faded = true;
        // The fade also covers a burst that started on the final target: the
        // four-frame scatter is shorter than this span.
        var fade = Track(Root.CreateTween().SetParallel());
        foreach (var cage in _cages.Values)
        {
            foreach (var material in cage.Materials)
            {
                fade.TweenMethod(
                    Callable.From<float>(value => material.SetShaderParameter("opacity", value)),
                    1f,
                    0f,
                    FadeDuration);
            }
        }

        fade.TweenProperty(Root, "position", Root.Position + Vector2.Up * SublimateLiftPx, FadeDuration)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);
        fade.TweenProperty(_backRoot, "position", _backRoot.Position + Vector2.Up * SublimateLiftPx, FadeDuration)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);
        fade.Chain().TweenCallback(Callable.From(DisposePresentation));
    }

    private void DisposePresentation()
    {
        ReleaseBackRoot();
        Dispose();
    }

    private void OnFreezeCombatEnded(CombatRoom _)
    {
        ReleaseBackRoot();
    }

    private void OnRootTreeExiting()
    {
        ReleaseBackRoot();
    }

    private void ReleaseBackRoot()
    {
        CombatManager.Instance.CombatEnded -= OnFreezeCombatEnded;
        Root.TreeExiting -= OnRootTreeExiting;
        if (GodotObject.IsInstanceValid(_backRoot) && !_backRoot.IsQueuedForDeletion())
            _backRoot.QueueFreeSafely();
    }

    private static (PackedScene Root, PackedScene Target) LoadScenes()
        => (PreloadManager.Cache.GetScene(ScenePath), PreloadManager.Cache.GetScene(TargetScenePath));

    private static void LogLoadFailure(Exception exception)
    {
        if (_loadFailureLogged)
            return;

        _loadFailureLogged = true;
        MainFile.Logger.Error(
            $"Could not create Freeze cage VFX from {ScenePath}, {TargetScenePath}, and {ShaderPath}: {exception}");
    }

    /// <summary>One target's two-layer ice-spine cage.</summary>
    private sealed class CageVisual
    {
        internal CageVisual(
            PackedScene scene,
            Node2D backParent,
            Node2D frontParent,
            CelVfxGeometry.TargetGeometry geometry,
            int index,
            FreezeWeight weight)
        {
            Back = CreateLayer(scene, backParent, geometry, index, weight, false);
            Front = CreateLayer(scene, frontParent, geometry, index, weight, true);
            Materials = [Back.Material, Front.Material];
        }

        internal LayerVisual Back { get; }
        internal LayerVisual Front { get; }
        internal Node2D BackRoot => Back.Root;
        internal Node2D FrontRoot => Front.Root;
        internal ShaderMaterial BackMaterial => Back.Material;
        internal ShaderMaterial FrontMaterial => Front.Material;
        internal IReadOnlyList<ShaderMaterial> Materials { get; }
        internal bool HasShattered { get; set; }

        private static LayerVisual CreateLayer(
            PackedScene scene,
            Node2D parent,
            CelVfxGeometry.TargetGeometry geometry,
            int index,
            FreezeWeight weight,
            bool foreground)
        {
            var root = scene.Instantiate<Node2D>();
            root.Name = $"FreezeCage{index + 1}{(foreground ? "Front" : "Back")}";
            root.ZAsRelative = true;
            root.ZIndex = foreground ? 1 : 0;
            parent.AddChildSafely(root);
            root.GlobalPosition = geometry.Center;
            root.Scale = Vector2.One;

            var body = root.GetNode<ColorRect>("%CageBody");
            var material = CelVfxGeometry.DuplicateMaterial(
                body,
                $"freeze cage {(foreground ? "front" : "back")} {index}");
            body.Size = geometry.Size;
            body.Position = -geometry.Size * 0.5f;
            material.SetShaderParameter("region_size", geometry.Size);
            material.SetShaderParameter("seed", index * 0.317f + 0.19f);
            material.SetShaderParameter("weight", WeightValue(weight));
            material.SetShaderParameter("layer_mode", foreground ? 1f : 0f);
            material.SetShaderParameter("rise", 0f);
            material.SetShaderParameter("shatter", 0f);
            material.SetShaderParameter("glint", 0f);
            material.SetShaderParameter("opacity", 1f);
            return new LayerVisual(root, material);
        }

        internal sealed record LayerVisual(Node2D Root, ShaderMaterial Material);
    }
}
