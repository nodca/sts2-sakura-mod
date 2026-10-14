using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.TestSupport;
using SakuraMod.SakuraModCode.FourthAct.Dark;
using SakuraMod.SakuraModCode.FourthAct.Wind;

namespace SakuraMod.SakuraModCode.FourthAct.Visuals;

internal enum SakuraStandeeSizeEffect
{
    Big,
    Little
}

internal static class SakuraStandeeSizeRules
{
    internal const float BigMultiplier = 1.25f;
    internal const float LittleMultiplier = 0.78f;

    internal static float Multiplier(bool bigActive, bool littleActive) =>
        (bigActive ? BigMultiplier : 1f) * (littleActive ? LittleMultiplier : 1f);

    internal static Vector2 RestScale(Vector2 baseScale, float multiplier) =>
        baseScale * multiplier;

    internal static Vector2 FootAnchoredPosition(
        Vector2 basePosition,
        float floorY,
        float verticalMultiplier) =>
        new(
            basePosition.X,
            floorY + (basePosition.Y - floorY) * verticalMultiplier);
}

internal enum SakuraStandeePlaybackPriority
{
    Idle,
    Action
}

internal sealed class SakuraStandeePlaybackState
{
    internal SakuraStandeePlaybackPriority Priority { get; private set; }
        = SakuraStandeePlaybackPriority.Idle;

    internal bool IsDisposed { get; private set; }

    private int Generation { get; set; }

    internal bool TryBegin(SakuraStandeePlaybackPriority priority, out int generation)
    {
        generation = Generation;
        if (IsDisposed || priority < Priority)
            return false;

        Priority = priority;
        generation = ++Generation;
        return true;
    }

    internal bool TryFinish(int generation)
    {
        if (!IsCurrent(generation))
            return false;

        Priority = SakuraStandeePlaybackPriority.Idle;
        return true;
    }

    internal bool IsCurrent(int generation) =>
        !IsDisposed && generation == Generation;

    internal void Dispose() => IsDisposed = true;
}

// Owns a static standee's transform: entry and idle motion for the Sakura-side standees,
// the persistent Big/Little size state, and texture swaps (fourth-act action poses, Fly
// frames). Fourth-act enemies attach with motion off; their acting lives in
// FourthActStandeeMaterial and the move effect layers.
internal sealed partial class SakuraStandeeActionController : Node
{
    internal const string NodeName = "SakuraStandeeActionController";

    private const float EntryOffsetY = 16f;
    private const float EntryDuration = 0.42f;
    private const float IdleLift = 3.4f;
    private const float IdleHalfDuration = 1.85f;
    private const float IdleTilt = 0.005f;

    private readonly Node2D _body;
    private readonly Vector2 _restPosition;
    private readonly Vector2 _restScale;
    private readonly float _floorY;
    private readonly Sprite2D? _sprite;
    private readonly bool _motion;
    private readonly bool _playIdleMotion;
    private readonly Dictionary<string, Texture2D> _textures = new(StringComparer.Ordinal);
    private readonly SakuraStandeePlaybackState _playback = new();
    private string _restTexturePath;
    private bool _posing;
    private Tween? _idleTween;
    private Tween? _clipTween;
    private TaskCompletionSource<bool>? _phaseCompletion;
    private bool _bigActive;
    private bool _littleActive;

    private SakuraStandeeActionController(
        Node2D body,
        Vector2 restPosition,
        Vector2 restScale,
        float floorY,
        string restTexturePath,
        bool motion,
        bool playIdleMotion)
    {
        _body = body;
        _restPosition = restPosition;
        _restScale = restScale;
        _floorY = floorY;
        _restTexturePath = restTexturePath;
        _motion = motion;
        _playIdleMotion = motion && playIdleMotion;
        _sprite = FindSprite(body);
        PreloadTextures(restTexturePath);
        Name = NodeName;
    }

    internal static void Attach(
        Node2D body,
        Vector2 restPosition,
        Vector2 restScale,
        float floorY,
        string restTexturePath,
        bool playIdleMotion = true,
        bool motion = true)
    {
        if (body.GetNodeOrNull<SakuraStandeeActionController>(NodeName) is not null)
            return;

        body.AddChild(new SakuraStandeeActionController(
            body,
            restPosition,
            restScale,
            floorY,
            restTexturePath,
            motion,
            playIdleMotion));
    }

    internal static SakuraStandeeActionController? TryGet(Creature creature) =>
        creature.GetCreatureNode() is { } node ? TryGet(node) : null;

    internal static SakuraStandeeActionController? TryGet(NCreature node) =>
        node.Visuals.GetNodeOrNull<Node2D>("%Visuals")
            ?.GetNodeOrNull<SakuraStandeeActionController>(NodeName);

    internal bool HasActionPose => ActionTexturePath(_restTexturePath) is not null;

    public override void _Ready()
    {
        TreeExiting += OnTreeExiting;
        if (_motion && !TestMode.IsOn)
            TaskHelper.RunSafely(PlayEntryAsync());
    }

    // An instant swap to the standee's action pose, held until released. No transform moves.
    internal void SetActionPose(bool posing)
    {
        _posing = posing;
        if (!posing)
        {
            ApplyRestTexture();
            return;
        }

        if (_sprite is not null
            && IsUsable(_sprite)
            && ActionTexturePath(_restTexturePath) is { } path
            && _textures.TryGetValue(path, out var texture))
            _sprite.Texture = texture;
    }

    internal async Task PlaySizeEffectAsync(
        SakuraStandeeSizeEffect effect,
        Func<Task> resolveAtContact)
    {
        ArgumentNullException.ThrowIfNull(resolveAtContact);

        var previousMultiplier = SizeMultiplier;
        ActivateSizeEffect(effect);
        var targetMultiplier = SizeMultiplier;
        if (!TryBegin(SakuraStandeePlaybackPriority.Action, out var generation))
        {
            await resolveAtContact();
            return;
        }

        try
        {
            var anticipationFactor = effect == SakuraStandeeSizeEffect.Big
                ? new Vector2(1.04f, 0.90f)
                : new Vector2(1.04f, 1.04f);
            var stillCurrent = await PlayPhaseAsync(
                generation,
                0.10,
                SakuraStandeeSizeRules.FootAnchoredPosition(
                    _restPosition,
                    _floorY,
                    previousMultiplier * anticipationFactor.Y),
                _restScale * previousMultiplier * anticipationFactor,
                0f,
                effect == SakuraStandeeSizeEffect.Big
                    ? new Color(1f, 0.91f, 0.68f, 0.94f)
                    : new Color(0.78f, 0.90f, 1f, 0.94f),
                Tween.TransitionType.Quad,
                Tween.EaseType.In);

            if (stillCurrent)
            {
                var contactFactor = effect == SakuraStandeeSizeEffect.Big ? 1.035f : 0.96f;
                stillCurrent = await PlayPhaseAsync(
                    generation,
                    effect == SakuraStandeeSizeEffect.Big ? 0.24 : 0.20,
                    SakuraStandeeSizeRules.FootAnchoredPosition(
                        _restPosition,
                        _floorY,
                        targetMultiplier * contactFactor),
                    _restScale * targetMultiplier * contactFactor,
                    0f,
                    Colors.White,
                    effect == SakuraStandeeSizeEffect.Big
                        ? Tween.TransitionType.Back
                        : Tween.TransitionType.Cubic,
                    Tween.EaseType.Out);
            }

            // Presentation interruption must never skip authoritative gameplay.
            await resolveAtContact();

            if (stillCurrent && IsCurrent(generation))
                await PlayRecoveryAsync(generation, 0.26);
        }
        finally
        {
            FinishClip(generation);
        }
    }

    internal void ApplySizeEffectState(SakuraStandeeSizeEffect effect)
    {
        ActivateSizeEffect(effect);
        if (TryBegin(SakuraStandeePlaybackPriority.Action, out var generation))
            FinishClip(generation);
    }

    internal async Task PlayTextureSequenceAsync(
        IEnumerable<string> frames,
        string finalTexturePath,
        double frameSeconds)
    {
        _restTexturePath = finalTexturePath;
        if (_sprite is null || !TryBegin(SakuraStandeePlaybackPriority.Action, out var generation))
        {
            ApplyRestTexture();
            return;
        }

        foreach (var frame in frames)
        {
            if (!IsCurrent(generation) || !IsUsable(_sprite))
                break;

            if (!_textures.TryGetValue(frame, out var texture))
                break;

            _sprite.Texture = texture;
            await _sprite.ToSignal(
                _sprite.GetTree().CreateTimer(frameSeconds),
                SceneTreeTimer.SignalName.Timeout);
        }

        if (IsCurrent(generation))
            ApplyRestTexture();
        FinishClip(generation);
    }

    private async Task PlayEntryAsync()
    {
        if (!TryBegin(SakuraStandeePlaybackPriority.Idle, out var generation))
            return;

        _body.Position = RestPosition + Vector2.Down * EntryOffsetY;
        _body.Rotation = 0f;
        _body.Scale = RestScale;
        _body.Modulate = new Color(1f, 1f, 1f, 0.86f);
        await PlayPhaseAsync(
            generation,
            EntryDuration,
            RestPosition,
            RestScale,
            0f,
            Colors.White,
            Tween.TransitionType.Cubic);
        FinishClip(generation);
    }

    private Task<bool> PlayRecoveryAsync(int generation, double duration = 0.18) =>
        PlayPhaseAsync(
            generation,
            duration,
            RestPosition,
            RestScale,
            0f,
            Colors.White,
            Tween.TransitionType.Cubic,
            Tween.EaseType.Out);

    private async Task<bool> PlayPhaseAsync(
        int generation,
        double duration,
        Vector2 position,
        Vector2 scale,
        float rotation,
        Color modulate,
        Tween.TransitionType transition = Tween.TransitionType.Quad,
        Tween.EaseType ease = Tween.EaseType.InOut)
    {
        if (!IsCurrent(generation) || !IsUsable(_body))
            return false;

        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _phaseCompletion = completion;
        var tween = _body.CreateTween().SetParallel();
        _clipTween = tween;
        tween.TweenProperty(_body, "position", position, duration).SetTrans(transition).SetEase(ease);
        tween.TweenProperty(_body, "scale", scale, duration).SetTrans(transition).SetEase(ease);
        tween.TweenProperty(_body, "rotation", rotation, duration).SetTrans(transition).SetEase(ease);
        tween.TweenProperty(_body, "modulate", modulate, duration).SetTrans(transition).SetEase(ease);
        tween.Chain().TweenCallback(Callable.From(() => completion.TrySetResult(IsCurrent(generation))));
        return await completion.Task;
    }

    private bool TryBegin(SakuraStandeePlaybackPriority priority, out int generation)
    {
        if (!_playback.TryBegin(priority, out generation))
            return false;

        CancelCurrent();
        StopIdle();
        return true;
    }

    private void FinishClip(int generation)
    {
        if (!_playback.TryFinish(generation))
            return;

        RestoreRestState();
        StartIdle();
    }

    private bool IsCurrent(int generation) =>
        _playback.IsCurrent(generation);

    private void CancelCurrent()
    {
        if (_clipTween is { } tween && tween.IsValid())
            tween.Kill();
        _clipTween = null;
        _phaseCompletion?.TrySetResult(false);
        _phaseCompletion = null;
    }

    private void RestoreRestState()
    {
        if (!IsUsable(_body))
            return;

        _body.Position = RestPosition;
        _body.Scale = RestScale;
        _body.Rotation = 0f;
        _body.Modulate = Colors.White;
        SetActionPose(_posing);
    }

    private void StartIdle()
    {
        if (!_playIdleMotion || _playback.IsDisposed || !IsUsable(_body))
            return;

        StopIdle();
        var liftedPosition = RestPosition + Vector2.Up * IdleLift;
        var settledPosition = RestPosition + Vector2.Down * (IdleLift * 0.35f);
        _idleTween = _body.CreateTween().SetLoops();
        _idleTween.TweenProperty(_body, "position", liftedPosition, IdleHalfDuration)
            .SetEase(Tween.EaseType.InOut).SetTrans(Tween.TransitionType.Sine);
        _idleTween.Parallel().TweenProperty(_body, "rotation", -IdleTilt, IdleHalfDuration)
            .SetEase(Tween.EaseType.InOut).SetTrans(Tween.TransitionType.Sine);
        _idleTween.TweenProperty(_body, "position", settledPosition, IdleHalfDuration)
            .SetEase(Tween.EaseType.InOut).SetTrans(Tween.TransitionType.Sine);
        _idleTween.Parallel().TweenProperty(_body, "rotation", IdleTilt * 0.55f, IdleHalfDuration)
            .SetEase(Tween.EaseType.InOut).SetTrans(Tween.TransitionType.Sine);
    }

    private void StopIdle()
    {
        if (_idleTween is { } tween && tween.IsValid())
            tween.Kill();
        _idleTween = null;
    }

    private float SizeMultiplier =>
        SakuraStandeeSizeRules.Multiplier(_bigActive, _littleActive);

    private Vector2 RestPosition =>
        SakuraStandeeSizeRules.FootAnchoredPosition(_restPosition, _floorY, SizeMultiplier);

    private Vector2 RestScale =>
        SakuraStandeeSizeRules.RestScale(_restScale, SizeMultiplier);

    private void ActivateSizeEffect(SakuraStandeeSizeEffect effect)
    {
        if (effect == SakuraStandeeSizeEffect.Big)
            _bigActive = true;
        else
            _littleActive = true;
    }

    private void ApplyRestTexture()
    {
        if (_sprite is not null
            && IsUsable(_sprite)
            && _textures.TryGetValue(_restTexturePath, out var texture))
            _sprite.Texture = texture;
    }

    private static string? ActionTexturePath(string restTexturePath)
    {
        if (restTexturePath == DarkEnemyAssets.Standee)
            return DarkEnemyAssets.Action;
        return WindEnemyAssets.ActionFramesFor(restTexturePath).FirstOrDefault();
    }

    private void PreloadTextures(string restTexturePath)
    {
        IEnumerable<string> paths = [restTexturePath];
        if (restTexturePath == WindEnemyAssets.FlyAirborne)
        {
            paths = paths
                .Append(WindEnemyAssets.FlyGrounded)
                .Concat(WindEnemyAssets.FlyTransitionFrames);
        }
        else if (restTexturePath == DarkEnemyAssets.Standee)
        {
            paths = paths.Append(DarkEnemyAssets.Action);
        }
        else
        {
            paths = paths.Concat(WindEnemyAssets.ActionFramesFor(restTexturePath));
        }

        foreach (var path in paths.Distinct(StringComparer.Ordinal))
        {
            if (ResourceLoader.Load<Texture2D>(path) is { } texture)
                _textures[path] = texture;
            else
                MainFile.Logger.Error($"Could not preload fourth-act standee texture {path}.");
        }
    }

    private void OnTreeExiting()
    {
        _playback.Dispose();
        TreeExiting -= OnTreeExiting;
        CancelCurrent();
        StopIdle();
    }

    private static bool IsUsable(GodotObject value) =>
        GodotObject.IsInstanceValid(value) && value is Node node && node.IsInsideTree();

    private static Sprite2D? FindSprite(Node root)
    {
        if (root is Sprite2D sprite)
            return sprite;
        foreach (var child in root.GetChildren())
        {
            if (FindSprite(child) is { } nested)
                return nested;
        }
        return null;
    }
}
