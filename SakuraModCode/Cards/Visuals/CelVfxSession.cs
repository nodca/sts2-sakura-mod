using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Pooling;
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
    private const float CardRiseDuration = 0.24f;
    private const float WandTapDownDuration = 0.08f;
    private const float WandTapRecoverDuration = 0.11f;
    private const float StandardPreludeLeadDuration = 0.18f;
    private const float CardFadeDuration = 0.12f;
    private const float WandPreludeHoldDuration = 2f / StepFrequency;
    private const float CardScale = 0.46f;
    private const float CardRiseDistance = 62f;
    private const float WandTapRadians = -0.12f;
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
    private float _cardRise;
    private Control? _preludeCardOrigin;
    private NCard? _preludeCard;
    private NCard? _standardPreludeCard;
    private NCard? _suppressedNativeCard;
    private CardModel? _suppressedNativeCardModel;
    private bool _suppressedNativeCardWasVisible;
    private ColorRect? _preludeLines;
    private ShaderMaterial? _preludeLineMaterial;
    private SakuraChibiWandRig? _wandRig;
    private float _wandRestRotation;
    private StringName? _pausedMicroAnimation;
    private double _pausedMicroPosition;
    private bool _resumeMicroAnimation;

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

    internal static void PreloadResources() => SakuraMagicCirclePresenter.PreloadResources();

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

        if (SakuraChibiStandeeIdleController.TryGet(casterNode) is { } chibiController
            && chibiController.TryGetWandPreludeRig(out var rig))
        {
            return await PlayChibiWandPrelude(card, casterNode, rig);
        }

        if (SakuraStandeeIdleController.TryGet(casterNode) is not null)
            return await PlayStandardPrelude(card, casterNode);

        return IsActive();
    }

    private async Task<bool> PlayChibiWandPrelude(
        CardModel card,
        NCreature casterNode,
        SakuraChibiWandRig rig)
    {
        try
        {
            CreateWandPrelude(card, casterNode, rig);
            TaskHelper.RunSafely(TrackWandPreludePosition());

            var rise = Track(_root.CreateTween());
            rise.TweenMethod(
                    Callable.From<float>(value => _cardRise = value),
                    0f,
                    1f,
                    CardRiseDuration)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Cubic);
            if (!await WaitActive(CardRiseDuration))
                return false;

            PauseMicroAnimation(rig);
            var tap = Track(rig.WandRoot.CreateTween());
            tap.TweenProperty(
                    rig.WandRoot,
                    "rotation",
                    _wandRestRotation + WandTapRadians,
                    WandTapDownDuration)
                .SetEase(Tween.EaseType.In)
                .SetTrans(Tween.TransitionType.Quad);
            if (!await WaitActive(WandTapDownDuration))
                return false;

            BeginWandPreludeHold();
            var recover = Track(rig.WandRoot.CreateTween());
            recover.TweenProperty(
                    rig.WandRoot,
                    "rotation",
                    _wandRestRotation,
                    WandTapRecoverDuration)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Back);
            if (_preludeCardOrigin is { } cardOrigin)
            {
                var cardFade = Track(_root.CreateTween());
                cardFade.TweenProperty(cardOrigin, "modulate:a", 0f, CardFadeDuration)
                    .SetEase(Tween.EaseType.InOut)
                    .SetTrans(Tween.TransitionType.Sine);
            }

            // The wand-card contact releases the card-specific effect. This session
            // retires only its card and lines; the room presenter owns the circle.
            TaskHelper.RunSafely(RetireWandPrelude(WandTapRecoverDuration));
            return IsActive();
        }
        catch (Exception exception)
        {
            MainFile.Logger.Error($"Cel VFX wand prelude failed for {card.Id}: {exception}");
            Dispose();
            return false;
        }
    }

    private async Task<bool> PlayStandardPrelude(CardModel card, NCreature casterNode)
    {
        if (!TryFindNativePlayedCard(card, out var nativeCard))
            return IsActive();

        try
        {
            CreateStandardPrelude(card, casterNode, nativeCard);
            TaskHelper.RunSafely(TrackWandPreludePosition());
            if (!await WaitActive(StandardPreludeLeadDuration))
                return false;

            BeginWandPreludeHold();
            TaskHelper.RunSafely(RetireWandPrelude(0f));
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

    private void CreateWandPrelude(CardModel card, NCreature casterNode, SakuraChibiWandRig rig)
    {
        CreatePreludeLayers();

        var cardOrigin = new Control
        {
            Name = "SakuraCelWandPreludeCard",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZAsRelative = false,
            ZIndex = PreludeZIndex
        };
        _room.CombatVfxContainer.AddChildSafely(cardOrigin);
        _preludeCardOrigin = cardOrigin;

        var preview = NCard.Create(card)
            ?? throw new InvalidOperationException($"Could not create Cel VFX card preview for {card.Id}.");
        _preludeCard = preview;
        cardOrigin.AddChildSafely(preview);
        preview.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
        preview.Position = Vector2.Zero;
        preview.Scale = Vector2.One * CardScale;
        preview.MouseFilter = Control.MouseFilterEnum.Ignore;
        _wandRig = rig;
        _wandRestRotation = rig.WandRoot.Rotation;
        _cardRise = 0f;
        UpdateWandPreludePosition();
        SuppressNativePlayedCard(card);
    }

    private void CreateStandardPrelude(CardModel card, NCreature casterNode, NCard nativeCard)
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

    private void SuppressNativePlayedCard(CardModel card)
    {
        if (_suppressedNativeCard is not null
            || !TryFindNativePlayedCard(card, out var nativeCard)
            || ReferenceEquals(nativeCard, _preludeCard)
            || !GodotObject.IsInstanceValid(nativeCard))
        {
            return;
        }

        _suppressedNativeCard = nativeCard;
        _suppressedNativeCardModel = card;
        _suppressedNativeCardWasVisible = nativeCard.Visible;
        nativeCard.Visible = false;
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

    private void RestoreNativePlayedCard()
    {
        var nativeCard = _suppressedNativeCard;
        var cardModel = _suppressedNativeCardModel;
        var wasVisible = _suppressedNativeCardWasVisible;
        _suppressedNativeCard = null;
        _suppressedNativeCardModel = null;

        if (nativeCard is not null
            && GodotObject.IsInstanceValid(nativeCard)
            && ReferenceEquals(nativeCard.Model, cardModel))
        {
            nativeCard.Visible = wasVisible;
        }
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

    private async Task RetireWandPrelude(float recoveryDuration)
    {
        if (recoveryDuration > 0f && !await WaitActive(recoveryDuration))
            return;
        RestoreWandRig();

        var remainingHold = WandPreludeHoldDuration - recoveryDuration;
        if (remainingHold > 0f && !await WaitActive(remainingHold))
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

        if (_wandRig is { } rig
            && GodotObject.IsInstanceValid(rig.Tip)
            && _preludeCardOrigin is { } cardOrigin)
        {
            var cardHeight = CardDisplaySize(_preludeCard?.Model).Y * CardScale;
            var tip = rig.Tip.GlobalPosition;
            var cardCenter = tip
                + Vector2.Up * cardHeight * 0.5f
                + Vector2.Down * CardRiseDistance * (1f - _cardRise);
            cardOrigin.GlobalPosition = cardCenter;
            // The card remains full-face and unmirrored. Only the world-space anchor
            // follows the flipped chibi rig, so left/right standee orientation cannot
            // invert the card art or inherit the rig's 0.28 scale.
            cardOrigin.Scale = Vector2.One;
            lines.GlobalPosition = cardCenter - lines.Size * 0.5f;
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

    private static Vector2 CardDisplaySize(CardModel? card)
    {
        if (card is not null
            && SakuraCardCatalog.TryGetMetadata(card, out var metadata)
            && metadata.Era == SourceEraClass.Clear)
        {
            return SakuraCardGeometry.ClearLayoutSize;
        }
        return SakuraCardGeometry.ClassicLayoutSize;
    }

    private void PauseMicroAnimation(SakuraChibiWandRig rig)
    {
        var player = rig.MicroAnimationPlayer;
        _resumeMicroAnimation = player.IsPlaying();
        _pausedMicroAnimation = player.CurrentAnimation;
        _pausedMicroPosition = player.CurrentAnimationPosition;
        if (_resumeMicroAnimation)
            player.Pause();
    }

    private void RestoreWandRig()
    {
        if (_wandRig is not { } rig)
            return;

        if (GodotObject.IsInstanceValid(rig.WandRoot))
            rig.WandRoot.Rotation = _wandRestRotation;
        if (_resumeMicroAnimation
            && GodotObject.IsInstanceValid(rig.MicroAnimationPlayer)
            && _pausedMicroAnimation is { } pausedAnimation
            && !pausedAnimation.IsEmpty)
        {
            rig.MicroAnimationPlayer.Play(pausedAnimation);
            rig.MicroAnimationPlayer.Seek(_pausedMicroPosition, update: true);
        }
        _wandRig = null;
        _resumeMicroAnimation = false;
    }

    private void ReleaseWandPrelude()
    {
        RestoreWandRig();
        _preludeLineMaterial = null;

        if (_preludeCard is { } card && GodotObject.IsInstanceValid(card))
        {
            card.GetParent()?.RemoveChild(card);
            NodePool.Free(card);
        }
        _preludeCard = null;
        _standardPreludeCard = null;

        if (_preludeCardOrigin is { } origin
            && GodotObject.IsInstanceValid(origin)
            && !origin.IsQueuedForDeletion())
        {
            origin.QueueFreeSafely();
        }
        _preludeCardOrigin = null;

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
        RestoreNativePlayedCard();
        if (queueFree
            && GodotObject.IsInstanceValid(_root)
            && !_root.IsQueuedForDeletion())
        {
            _root.QueueFreeSafely();
        }
    }
}
