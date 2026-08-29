using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.TestSupport;
using SakuraMod.SakuraModCode.Character;

namespace SakuraMod.SakuraModCode.Cards;

internal abstract class CelVfxSession : IDisposable
{
    internal const string WandPreludeShaderPath =
        SakuraMagicCirclePresenter.WandPreludeShaderPath;
    internal const string MagicCircleInkPath =
        SakuraMagicCirclePresenter.MagicCircleInkPath;
    internal const string MagicCircleKnockoutPath =
        SakuraMagicCirclePresenter.MagicCircleKnockoutPath;
    internal static IReadOnlyList<string> SharedAssetPaths { get; } =
        SakuraMagicCirclePresenter.AssetPaths;

    /// <summary>
    /// The shared stepped-clock rate, mirroring <c>CEL_STEP_HZ</c> in
    /// <c>cel_vfx.gdshaderinc</c>. Visible to derived sessions because any beat or
    /// oscillation a card builds is bounded by it — a subclass restating 12 here
    /// would be a second owner of the value the shaders already fixed.
    /// </summary>
    protected const float StepFrequency = 12f;
    private const float StandardPreludeLeadDuration = 0.18f;
    private const float WandPreludeHoldDuration = 2f / StepFrequency;
    private const float SpeedLineDiameter = 560f;
    private const int PreludeZIndex = 4000;

    private readonly Node2D _root;
    private readonly NCombatRoom _room;
    private readonly List<Tween> _tweens = [];
    private bool _clockStarted;
    private bool _disposed;
    private float _elapsed;
    private float _wallElapsed;
    private float _holdRemaining;
    private float _holdAt;
    private float _preludeElapsed;
    private float _preludeHoldRemaining;
    private float _preludeHoldAt;
    private NCard? _standardPreludeCard;
    private ColorRect? _preludeLines;
    private ShaderMaterial? _preludeLineMaterial;

    protected CelVfxSession(Node2D root, NCombatRoom room)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _room = room ?? throw new ArgumentNullException(nameof(room));
        CombatManager.Instance.CombatEnded += OnCombatEnded;
        _root.TreeExiting += OnTreeExiting;
    }

    protected abstract IEnumerable<ShaderMaterial> Materials { get; }
    protected abstract float MaximumLifetime { get; }

    protected Node2D Root => _root;
    protected NCombatRoom Room => _room;

    internal static bool ShouldPlayCelPrelude(CardModel card) =>
        !TestMode.IsOn
        && SakuraCardCatalog.TryGetMetadata(card, out var metadata)
        && metadata.Era.HasValue;

    /// <summary>
    /// Runs one presentation session around authoritative gameplay. Presentation
    /// failures make the cue scope inert; they never suppress or repeat gameplay.
    /// </summary>
    internal static Task PlayOrResolveAsync<TSession>(
        string label,
        Func<TSession?> tryCreate,
        Func<TSession, Task<bool>> playPrelude,
        Func<CueScope<TSession>, Task> resolveGameplay,
        Action<TSession> beginOutro,
        Action<TSession> dispose,
        Action<string, Exception>? reportFailure = null)
        where TSession : class
        => PlayOrResolveAsync(
            SakuraModConfig.IsCardVfxEnabled(),
            label,
            tryCreate,
            playPrelude,
            resolveGameplay,
            beginOutro,
            dispose,
            reportFailure);

    internal static async Task PlayOrResolveAsync<TSession>(
        bool presentationEnabled,
        string label,
        Func<TSession?> tryCreate,
        Func<TSession, Task<bool>> playPrelude,
        Func<CueScope<TSession>, Task> resolveGameplay,
        Action<TSession> beginOutro,
        Action<TSession> dispose,
        Action<string, Exception>? reportFailure = null)
        where TSession : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(tryCreate);
        ArgumentNullException.ThrowIfNull(playPrelude);
        ArgumentNullException.ThrowIfNull(resolveGameplay);
        ArgumentNullException.ThrowIfNull(beginOutro);
        ArgumentNullException.ThrowIfNull(dispose);

        reportFailure ??= (stage, exception) =>
            MainFile.Logger.Error($"Cel VFX {label} {stage} failed: {exception}");

        TSession? session = null;
        try
        {
            if (presentationEnabled)
                session = tryCreate();
        }
        catch (Exception exception)
        {
            ReportSafely(reportFailure, "create", exception);
        }

        var cues = new CueScope<TSession>(session, playPrelude, beginOutro, dispose, reportFailure);
        await cues.PrepareAsync();

        try
        {
            await resolveGameplay(cues);
        }
        catch
        {
            // Cleanup is presentation work. It must not replace an authoritative
            // gameplay exception, even if cleanup itself fails.
            cues.Abort();
            throw;
        }

        cues.Finish();
    }

    private static void ReportSafely(
        Action<string, Exception> reportFailure,
        string stage,
        Exception exception)
    {
        try
        {
            reportFailure(stage, exception);
        }
        catch
        {
            // Diagnostics are best-effort and may not alter combat resolution.
        }
    }

    internal sealed class CueScope<TSession>
        where TSession : class
    {
        private readonly Func<TSession, Task<bool>> _playPrelude;
        private readonly Action<TSession> _beginOutro;
        private readonly Action<TSession> _dispose;
        private readonly Action<string, Exception> _reportFailure;
        private TSession? _session;

        internal CueScope(
            TSession? session,
            Func<TSession, Task<bool>> playPrelude,
            Action<TSession> beginOutro,
            Action<TSession> dispose,
            Action<string, Exception> reportFailure)
        {
            _session = session;
            _playPrelude = playPrelude;
            _beginOutro = beginOutro;
            _dispose = dispose;
            _reportFailure = reportFailure;
        }

        internal async Task PrepareAsync()
        {
            if (_session is not { } session)
                return;

            try
            {
                if (await _playPrelude(session))
                    return;
            }
            catch (Exception exception)
            {
                ReportSafely(_reportFailure, "prelude", exception);
            }

            _session = null;
            DisposeSafely(session);
        }

        internal void Invoke(string cueName, Action<TSession> cue)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(cueName);
            ArgumentNullException.ThrowIfNull(cue);
            if (_session is not { } session)
                return;

            try
            {
                cue(session);
            }
            catch (Exception exception)
            {
                // A failed cue invalidates this presentation. Later cues become
                // no-ops, while the gameplay callback continues normally.
                _session = null;
                ReportSafely(_reportFailure, cueName, exception);
                DisposeSafely(session);
            }
        }

        /// <summary>
        /// The awaitable counterpart of <see cref="Invoke"/>, for a cue the
        /// gameplay callback waits on.
        /// </summary>
        /// <remarks>
        /// Arrow needs this because its contact flash has to land on the frame
        /// the damage resolves, and only the caller awaiting the cue can hold
        /// damage back until the arrow arrives. The degradation is identical to
        /// <see cref="Invoke"/>: a failed or absent session makes the cue a
        /// no-op that returns immediately, never a wait that only exists when
        /// presentation does. Waiting inside the presentation layer would give
        /// players with card VFX on a longer combat than players with it off.
        /// </remarks>
        internal async Task InvokeAsync(string cueName, Func<TSession, Task> cue)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(cueName);
            ArgumentNullException.ThrowIfNull(cue);
            if (_session is not { } session)
                return;

            try
            {
                await cue(session);
            }
            catch (Exception exception)
            {
                _session = null;
                ReportSafely(_reportFailure, cueName, exception);
                DisposeSafely(session);
            }
        }

        internal void Finish()
        {
            if (_session is not { } session)
                return;

            _session = null;
            try
            {
                _beginOutro(session);
            }
            catch (Exception exception)
            {
                ReportSafely(_reportFailure, "outro", exception);
                DisposeSafely(session);
            }
        }

        internal void Abort()
        {
            if (_session is not { } session)
                return;

            _session = null;
            DisposeSafely(session);
        }

        private void DisposeSafely(TSession session)
        {
            try
            {
                _dispose(session);
            }
            catch (Exception exception)
            {
                ReportSafely(_reportFailure, "cleanup", exception);
            }
        }
    }

    /// <summary>
    /// The two magic-circle masks, from the preload if it ran and from disk if it
    /// did not.
    /// </summary>
    /// <remarks>
    /// Visible to derived sessions because the circle is not the only consumer: a
    /// card whose own surface carries the seal as a face pattern needs the same two
    /// textures, and the alternative is each such card restating this fallback. The
    /// pair is loaded together because the source-order composition is meaningless
    /// with only one of them.
    /// </remarks>
    protected static (Texture2D Ink, Texture2D Knockout) LoadMagicCircleMasks()
    {
        var resources = SakuraMagicCirclePresenter.LoadResources();
        return (resources.Ink, resources.Knockout);
    }

    protected static bool TryPrepare<TResources>(
        string label,
        Func<TResources> loadResources,
        out NCombatRoom room,
        out Control container,
        out TResources resources)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(loadResources);
        room = null!;
        container = null!;
        resources = default!;
        if (TestMode.IsOn
            || NCombatRoom.Instance is not { } currentRoom
            || currentRoom.CombatVfxContainer is not { } currentContainer)
        {
            return false;
        }

        try
        {
            var loaded = loadResources();
            if (loaded is null)
                throw new InvalidOperationException($"Cel VFX {label} resource loader returned null.");
            room = currentRoom;
            container = currentContainer;
            resources = loaded;
            return true;
        }
        catch (Exception exception)
        {
            MainFile.Logger.Error($"Could not prepare Cel VFX {label} resources: {exception}");
            return false;
        }
    }

    internal void StartClock()
    {
        if (_clockStarted || _disposed)
            return;
        if (!float.IsFinite(MaximumLifetime) || MaximumLifetime <= 0f)
            throw new InvalidOperationException("Cel VFX MaximumLifetime must be finite and positive.");

        _clockStarted = true;
        TaskHelper.RunSafely(DriveClock());
    }

    protected void BeginHold(int holdSteps = 2)
    {
        if (!IsActive())
            return;
        if (holdSteps is < 2 or > 3)
            throw new ArgumentOutOfRangeException(nameof(holdSteps), "A cel hold must last two or three stepped frames.");

        if (_holdRemaining <= 0f)
            _holdAt = _elapsed;
        _holdRemaining = Math.Max(_holdRemaining, holdSteps / StepFrequency);
        ApplyClockUniforms();
    }

    protected async Task<bool> PlayCelPrelude(CardModel card, Creature? caster)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (!ShouldPlayCelPrelude(card)
            || caster is null
            || _room.GetCreatureNode(caster) is not { } casterNode)
        {
            return IsActive();
        }

        if (SakuraChibiStandeeIdleController.TryGet(casterNode) is not null
            || SakuraStandeeIdleController.TryGet(casterNode) is not null)
            return await PlayStandardPrelude(card);

        return IsActive();
    }

    private async Task<bool> PlayStandardPrelude(CardModel card)
    {
        if (!TryFindNativePlayedCard(card, out var nativeCard))
            return IsActive();

        try
        {
            CreateStandardPrelude(nativeCard);
            TaskHelper.RunSafely(TrackWandPreludePosition());
            if (!await WaitActive(StandardPreludeLeadDuration))
                return false;

            BeginWandPreludeHold();
            TaskHelper.RunSafely(RetireWandPrelude());
            return IsActive();
        }
        catch (Exception exception)
        {
            MainFile.Logger.Error($"Cel VFX standard prelude failed for {card.Id}: {exception}");
            Dispose();
            return false;
        }
    }

    protected async Task<bool> WaitActive(float seconds)
    {
        try
        {
            var waited = 0f;
            while (waited < seconds)
            {
                if (!IsActive())
                    return false;
                waited += await _root.AwaitProcessFrame();
            }
            return IsActive();
        }
        catch (OperationCanceledException) when (!IsActive())
        {
            return false;
        }
    }

    protected Tween Track(Tween tween)
    {
        ArgumentNullException.ThrowIfNull(tween);
        _tweens.Add(tween);
        return tween;
    }

    protected bool IsActive() =>
        !_disposed
        && !CombatManager.Instance.IsEnding
        && GodotObject.IsInstanceValid(_root)
        && _root.IsInsideTree()
        && !_root.IsQueuedForDeletion();

    private async Task DriveClock()
    {
        try
        {
            while (IsActive() && _wallElapsed < MaximumLifetime)
            {
                ApplyClockUniforms();
                var delta = await _root.AwaitProcessFrame();
                _wallElapsed += delta;
                _preludeElapsed += delta;
                if (_preludeHoldRemaining > 0f)
                    _preludeHoldRemaining = Math.Max(0f, _preludeHoldRemaining - delta);
                if (_holdRemaining > 0f)
                    _holdRemaining = Math.Max(0f, _holdRemaining - delta);
                else
                    _elapsed += delta;
            }

            if (IsActive())
                Dispose();
        }
        catch (OperationCanceledException) when (!IsActive())
        {
            // Leaving the tree cancels frame waits; TreeExiting owns cleanup.
        }
        catch (Exception exception)
        {
            MainFile.Logger.Error($"Cel VFX clock failed and was disposed: {exception}");
            Dispose();
        }
    }

    private void ApplyClockUniforms()
    {
        var held = _holdRemaining > 0f ? 1f : 0f;
        foreach (var material in Materials)
            ApplyClockUniforms(material, _elapsed, held, _holdAt);

        var preludeHeld = _preludeHoldRemaining > 0f ? 1f : 0f;
        ApplyClockUniforms(_preludeLineMaterial, _preludeElapsed, preludeHeld, _preludeHoldAt);
    }

    private static void ApplyClockUniforms(
        ShaderMaterial? material,
        float elapsed,
        float held,
        float heldAt)
    {
        if (material is null || !GodotObject.IsInstanceValid(material))
            return;

        material.SetShaderParameter("elapsed", elapsed);
        material.SetShaderParameter("held", held);
        material.SetShaderParameter("held_at", heldAt);
    }

    private void CreateStandardPrelude(NCard nativeCard)
    {
        CreatePreludeLayers();
        _standardPreludeCard = nativeCard;
        UpdateWandPreludePosition();
    }

    private void CreatePreludeLayers()
    {
        var container = _room.CombatVfxContainer;
        var (shader, magicCircleInk, magicCircleKnockout) =
            SakuraMagicCirclePresenter.LoadResources();

        var lines = new ColorRect
        {
            Name = "SakuraCelWandPreludeLines",
            Size = Vector2.One * SpeedLineDiameter,
            Position = Vector2.One * -SpeedLineDiameter * 0.5f,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZAsRelative = false,
            ZIndex = PreludeZIndex - 1,
            Material = new ShaderMaterial { Shader = shader }
        };
        container.AddChildSafely(lines);
        _preludeLines = lines;
        _preludeLineMaterial = CelVfxGeometry.DuplicateMaterial(lines, "wand prelude lines");
        _preludeLineMaterial.SetShaderParameter("region_size", lines.Size);
        _preludeLineMaterial.SetShaderParameter("magic_circle_ink", magicCircleInk);
        _preludeLineMaterial.SetShaderParameter("magic_circle_knockout", magicCircleKnockout);
        _preludeLineMaterial.SetShaderParameter("magic_circle_enabled", 0f);
        _preludeLineMaterial.SetShaderParameter("magic_circle_visibility", 0f);
        _preludeLineMaterial.SetShaderParameter("speed_lines_enabled", 1f);
    }

    private bool TryFindNativePlayedCard(CardModel card, out NCard nativeCard)
    {
        nativeCard = null!;
        if (_room.Ui is not { } ui
            || NCard.FindOnTable(card) is not { } foundCard
            || !GodotObject.IsInstanceValid(foundCard)
            || !ui.PlayContainer.IsAncestorOf(foundCard))
        {
            return false;
        }

        nativeCard = foundCard;
        return true;
    }

    private void BeginWandPreludeHold()
    {
        if (!IsActive())
            return;

        if (_preludeHoldRemaining <= 0f)
            _preludeHoldAt = _preludeElapsed;
        _preludeHoldRemaining = Math.Max(_preludeHoldRemaining, WandPreludeHoldDuration);
        ApplyClockUniforms();
    }

    private async Task RetireWandPrelude()
    {
        if (!await WaitActive(WandPreludeHoldDuration))
            return;
        ReleaseWandPrelude();
    }

    /// <summary>
    /// The era's seal-ink colour for a card, or null when the card has no era.
    /// </summary>
    /// <remarks>
    /// Card-to-colour rather than era-to-colour so a derived session drawing the
    /// seal's ink on its own surface never has to reach into
    /// <c>SakuraCardCatalog</c> itself. Era resolution and the palette then keep a
    /// single owner, which is what stops a card from shipping its own idea of what
    /// Clow gold is.
    /// </remarks>
    protected static Color? MagicCircleInkColour(CardModel card) =>
        SakuraCardCatalog.TryGetMetadata(card, out var metadata) && metadata.Era is { } era
            ? SakuraMagicCirclePresenter.ColourFor(era)
            : null;

    private async Task TrackWandPreludePosition()
    {
        try
        {
            while (IsActive() && _preludeLines is not null)
            {
                UpdateWandPreludePosition();
                await _root.AwaitProcessFrame();
            }
        }
        catch (OperationCanceledException) when (!IsActive())
        {
        }
    }

    private void UpdateWandPreludePosition()
    {
        if (_preludeLines is not { } lines)
        {
            return;
        }

        if (_standardPreludeCard is { } nativeCard
            && GodotObject.IsInstanceValid(nativeCard)
            && nativeCard.IsInsideTree())
        {
            var cardCenter = nativeCard.GetGlobalTransform()
                * (nativeCard.GetCurrentSize() * 0.5f);
            lines.GlobalPosition = cardCenter - lines.Size * 0.5f;
        }
    }

    private void ReleaseWandPrelude()
    {
        _preludeLineMaterial = null;
        _standardPreludeCard = null;

        if (_preludeLines is { } lines
            && GodotObject.IsInstanceValid(lines)
            && !lines.IsQueuedForDeletion())
        {
            lines.QueueFreeSafely();
        }
        _preludeLines = null;
    }

    private void OnCombatEnded(CombatRoom _) => Dispose();

    private void OnTreeExiting()
    {
        if (!_disposed)
            Dispose(queueFree: false);
    }

    public void Dispose() => Dispose(queueFree: true);

    private void Dispose(bool queueFree)
    {
        if (_disposed)
            return;

        _disposed = true;
        CombatManager.Instance.CombatEnded -= OnCombatEnded;
        if (GodotObject.IsInstanceValid(_root))
            _root.TreeExiting -= OnTreeExiting;
        foreach (var tween in _tweens)
        {
            if (GodotObject.IsInstanceValid(tween) && tween.IsValid())
                tween.Kill();
        }
        _tweens.Clear();
        ReleaseWandPrelude();
        if (queueFree
            && GodotObject.IsInstanceValid(_root)
            && !_root.IsQueuedForDeletion())
        {
            _root.QueueFreeSafely();
        }
    }
}
