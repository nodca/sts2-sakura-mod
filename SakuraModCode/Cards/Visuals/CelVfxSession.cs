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
        MainFile.ResPath + "/shaders/card_vfx/cel_wand_prelude.gdshader";
    internal const string MagicCircleInkPath =
        MainFile.ResPath + "/images/card_vfx/magic_circles/magic_circle_ink.png";
    internal const string MagicCircleKnockoutPath =
        MainFile.ResPath + "/images/card_vfx/magic_circles/magic_circle_knockout.png";

    private const float StepFrequency = 12f;
    private const float CardRiseDuration = 0.24f;
    private const float WandTapDownDuration = 0.08f;
    private const float WandTapRecoverDuration = 0.11f;
    private const float MagicCircleFadeInDuration = 0.18f;
    private const float MagicCircleSustainDuration = 0.14f;
    private const float MagicCircleFadeOutDuration = 0.24f;
    private const float CardFadeDuration = 0.12f;
    private const float WandPreludeHoldDuration = 2f / StepFrequency;
    private const float CardScale = 0.46f;
    private const float CardRiseDistance = 62f;
    private const float WandTapRadians = -0.12f;
    private const float SpeedLineDiameter = 560f;
    private const float MagicCircleDiameter = 760f;
    private const float MagicCircleRadius = 340f;
    private const float MagicCircleEnterScale = 0.78f;
    private const float MagicCircleExitScale = 0.82f;
    private const float MagicCircleFloorBias = 0.62f;
    private const int PreludeZIndex = 4000;
    private const int MagicCircleZIndex = -1;

    private static Shader? _wandPreludeShader;
    private static Texture2D? _magicCircleInk;
    private static Texture2D? _magicCircleKnockout;

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
    private Node2D? _magicCircleAnchor;
    private ColorRect? _magicCircle;
    private ShaderMaterial? _magicCircleMaterial;
    private NCreature? _preludeCasterNode;
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

    internal static void PreloadResources()
    {
        if (TestMode.IsOn)
            return;

        _wandPreludeShader = ResourceLoader.Load<Shader>(
            WandPreludeShaderPath,
            null,
            ResourceLoader.CacheMode.Reuse)
            ?? throw new InvalidOperationException($"Could not preload {WandPreludeShaderPath}.");
        _magicCircleInk = ResourceLoader.Load<Texture2D>(
            MagicCircleInkPath,
            null,
            ResourceLoader.CacheMode.Reuse)
            ?? throw new InvalidOperationException($"Could not preload {MagicCircleInkPath}.");
        _magicCircleKnockout = ResourceLoader.Load<Texture2D>(
            MagicCircleKnockoutPath,
            null,
            ResourceLoader.CacheMode.Reuse)
            ?? throw new InvalidOperationException($"Could not preload {MagicCircleKnockoutPath}.");
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
            TrackMagicCircleVisibilityTween(0f, 1f, MagicCircleFadeInDuration);
            TrackMagicCircleScaleTween(MagicCircleEnterScale, 1f, MagicCircleFadeInDuration);

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

            // The wand-card contact releases the card-specific effect. Card and
            // wand retire immediately, while the independent circle stays alive
            // long enough to overlap the physical field before fading itself.
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
            TrackMagicCircleVisibilityTween(0f, 1f, MagicCircleFadeInDuration);
            TrackMagicCircleScaleTween(MagicCircleEnterScale, 1f, MagicCircleFadeInDuration);
            if (!await WaitActive(MagicCircleFadeInDuration))
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
        ApplyClockUniforms(_magicCircleMaterial, _preludeElapsed, 0f, _preludeElapsed);
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
        CreatePreludeLayers(card, casterNode);

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
        CreatePreludeLayers(card, casterNode);
        _standardPreludeCard = nativeCard;
        UpdateWandPreludePosition();
    }

    private void CreatePreludeLayers(CardModel card, NCreature casterNode)
    {
        var container = _room.CombatVfxContainer;
        var shader = _wandPreludeShader
            ?? ResourceLoader.Load<Shader>(
                WandPreludeShaderPath,
                null,
                ResourceLoader.CacheMode.Reuse)
            ?? throw new InvalidOperationException($"Could not load {WandPreludeShaderPath}.");
        if (!SakuraCardCatalog.TryGetMetadata(card, out var metadata)
            || metadata.Era is not { } era)
        {
            throw new InvalidOperationException($"Card {card.Id} has no magic-circle era.");
        }
        var magicCircleInk = _magicCircleInk
            ?? ResourceLoader.Load<Texture2D>(MagicCircleInkPath)
            ?? throw new InvalidOperationException($"Could not load {MagicCircleInkPath}.");
        var magicCircleKnockout = _magicCircleKnockout
            ?? ResourceLoader.Load<Texture2D>(MagicCircleKnockoutPath)
            ?? throw new InvalidOperationException($"Could not load {MagicCircleKnockoutPath}.");

        var magicCircleAnchor = new Node2D
        {
            Name = "SakuraCelWandPreludeMagicCircleAnchor",
            ZAsRelative = false,
            ZIndex = MagicCircleZIndex
        };
        container.AddChildSafely(magicCircleAnchor);
        _magicCircleAnchor = magicCircleAnchor;

        var magicCircle = new ColorRect
        {
            Name = "SakuraCelWandPreludeMagicCircle",
            Size = Vector2.One * MagicCircleDiameter,
            Position = Vector2.One * MagicCircleDiameter * -0.5f,
            PivotOffset = Vector2.One * MagicCircleDiameter * 0.5f,
            Scale = Vector2.One * MagicCircleEnterScale,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Material = new ShaderMaterial { Shader = shader }
        };
        magicCircleAnchor.AddChildSafely(magicCircle);
        _magicCircle = magicCircle;
        _magicCircleMaterial = CelVfxGeometry.DuplicateMaterial(magicCircle, "wand prelude magic circle");
        _magicCircleMaterial.SetShaderParameter("region_size", magicCircle.Size);
        _magicCircleMaterial.SetShaderParameter("magic_circle_ink", magicCircleInk);
        _magicCircleMaterial.SetShaderParameter("magic_circle_knockout", magicCircleKnockout);
        _magicCircleMaterial.SetShaderParameter("magic_circle_colour", MagicCircleColour(era));
        _magicCircleMaterial.SetShaderParameter("magic_circle_enabled", 1f);
        _magicCircleMaterial.SetShaderParameter("magic_circle_visibility", 0f);
        _magicCircleMaterial.SetShaderParameter("magic_circle_radius", MagicCircleRadius);
        _magicCircleMaterial.SetShaderParameter("speed_lines_enabled", 0f);

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
        _preludeCasterNode = casterNode;
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

    private void TrackMagicCircleVisibilityTween(float from, float to, float duration)
    {
        var tween = Track(_root.CreateTween());
        tween.TweenMethod(
                Callable.From<float>(SetMagicCircleVisibility),
                from,
                to,
                duration)
            .SetEase(Tween.EaseType.InOut)
            .SetTrans(Tween.TransitionType.Sine);
    }

    private void TrackMagicCircleScaleTween(float from, float to, float duration)
    {
        if (_magicCircle is not { } circle || !GodotObject.IsInstanceValid(circle))
            return;

        circle.Scale = Vector2.One * from;
        var tween = Track(_root.CreateTween());
        tween.TweenProperty(circle, "scale", Vector2.One * to, duration)
            .SetEase(Tween.EaseType.InOut)
            .SetTrans(Tween.TransitionType.Sine);
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
        if (!await WaitActive(MagicCircleSustainDuration))
            return;

        TrackMagicCircleVisibilityTween(1f, 0f, MagicCircleFadeOutDuration);
        TrackMagicCircleScaleTween(1f, MagicCircleExitScale, MagicCircleFadeOutDuration);
        if (!await WaitActive(MagicCircleFadeOutDuration))
            return;
        ReleaseWandPrelude();
    }

    private void SetMagicCircleVisibility(float visibility)
    {
        if (_magicCircleMaterial is { } material && GodotObject.IsInstanceValid(material))
            material.SetShaderParameter("magic_circle_visibility", Mathf.Clamp(visibility, 0f, 1f));
    }

    private static Color MagicCircleColour(SourceEraClass era) => era switch
    {
        SourceEraClass.Clow => new Color(1f, 0.94f, 0.62f),
        SourceEraClass.Sakura => new Color(1f, 0.78f, 0.94f),
        SourceEraClass.Clear => new Color(0.88f, 1f, 0.8f),
        _ => throw new ArgumentOutOfRangeException(nameof(era), era, "Unknown magic-circle era.")
    };

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
        if (_magicCircleAnchor is { } magicCircleAnchor
            && _preludeCasterNode is { } casterNode
            && GodotObject.IsInstanceValid(magicCircleAnchor)
            && GodotObject.IsInstanceValid(casterNode))
        {
            magicCircleAnchor.GlobalPosition = ResolveMagicCircleCenter(_preludeCasterNode);
        }

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

    private static Vector2 ResolveMagicCircleCenter(NCreature? casterNode)
    {
        if (casterNode is null || !GodotObject.IsInstanceValid(casterNode))
            return Vector2.Zero;

        if (casterNode.Hitbox is { } hitbox && GodotObject.IsInstanceValid(hitbox))
        {
            var rect = hitbox.GetGlobalRect();
            if (rect.Size.X > 1f && rect.Size.Y > 1f)
            {
                var bodyCenter = rect.GetCenter();
                var floor = casterNode.GetBottomOfHitbox();
                return new Vector2(
                    bodyCenter.X,
                    Mathf.Lerp(bodyCenter.Y, floor.Y, MagicCircleFloorBias));
            }
        }

        return casterNode.VfxSpawnPosition + Vector2.Down * 64f;
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
        _magicCircleMaterial = null;
        _preludeCasterNode = null;

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

        if (_magicCircleAnchor is { } magicCircleAnchor
            && GodotObject.IsInstanceValid(magicCircleAnchor)
            && !magicCircleAnchor.IsQueuedForDeletion())
        {
            magicCircleAnchor.QueueFreeSafely();
        }
        _magicCircleAnchor = null;
        _magicCircle = null;
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
