using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.TestSupport;
using SakuraMod.SakuraModCode.FourthAct.Dark;
using SakuraMod.SakuraModCode.FourthAct.Dark.Models;
using SakuraMod.SakuraModCode.FourthAct.Wind;
using SakuraMod.SakuraModCode.FourthAct.Wind.Models;

namespace SakuraMod.SakuraModCode.FourthAct.Visuals;

internal enum SakuraStandeeClip
{
    Attack,
    Cast,
    Buff,
    Summon
}

internal enum SakuraStandeePlaybackPriority
{
    Idle,
    Action,
    Hurt,
    Death
}

internal sealed class SakuraStandeePlaybackState
{
    internal SakuraStandeePlaybackPriority Priority { get; private set; }
        = SakuraStandeePlaybackPriority.Idle;

    internal bool IsDead { get; private set; }
    internal bool IsDisposed { get; private set; }
    internal bool CanPlayNonDeath => !IsDead && !IsDisposed;

    private int Generation { get; set; }

    internal bool TryBegin(SakuraStandeePlaybackPriority priority, out int generation)
    {
        generation = Generation;
        if (IsDisposed
            || IsDead && priority != SakuraStandeePlaybackPriority.Death
            || priority < Priority)
            return false;

        Priority = priority;
        generation = ++Generation;
        return true;
    }

    internal bool TryBeginDeath(out int generation)
    {
        generation = Generation;
        if (IsDead || IsDisposed)
            return false;

        IsDead = true;
        return TryBegin(SakuraStandeePlaybackPriority.Death, out generation);
    }

    internal bool TryFinish(int generation)
    {
        if (!IsCurrent(generation) || IsDead)
            return false;

        Priority = SakuraStandeePlaybackPriority.Idle;
        return true;
    }

    internal bool IsCurrent(int generation) =>
        !IsDisposed && generation == Generation;

    internal void Dispose() => IsDisposed = true;
}

internal sealed partial class SakuraStandeeActionController : Node
{
    internal const string NodeName = "SakuraStandeeActionController";
    internal const float DeathDuration = 0.55f;

    private const float EntryOffsetY = 16f;
    private const float EntryDuration = 0.42f;
    private const float IdleLift = 3.4f;
    private const float IdleHalfDuration = 1.85f;
    private const float IdleTilt = 0.005f;

    private readonly Node2D _body;
    private readonly Vector2 _restPosition;
    private readonly Vector2 _restScale;
    private readonly Sprite2D? _sprite;
    private readonly bool _playIdleMotion;
    private readonly Dictionary<string, Texture2D> _textures = new(StringComparer.Ordinal);
    private readonly HashSet<Sprite2D> _afterimages = [];
    private readonly SakuraStandeePlaybackState _playback = new();
    private string _restTexturePath;
    private Tween? _idleTween;
    private Tween? _clipTween;
    private TaskCompletionSource<bool>? _phaseCompletion;

    private SakuraStandeeActionController(
        Node2D body,
        Vector2 restPosition,
        Vector2 restScale,
        string restTexturePath,
        bool playIdleMotion)
    {
        _body = body;
        _restPosition = restPosition;
        _restScale = restScale;
        _restTexturePath = restTexturePath;
        _playIdleMotion = playIdleMotion;
        _sprite = FindSprite(body);
        PreloadTextures(restTexturePath);
        Name = NodeName;
    }

    internal static void Attach(
        Node2D body,
        Vector2 restPosition,
        Vector2 restScale,
        string restTexturePath,
        bool playIdleMotion = true)
    {
        if (body.GetNodeOrNull<SakuraStandeeActionController>(NodeName) is not null)
            return;

        body.AddChild(new SakuraStandeeActionController(
            body,
            restPosition,
            restScale,
            restTexturePath,
            playIdleMotion));
    }

    internal static SakuraStandeeActionController? TryGet(Creature creature) =>
        creature.GetCreatureNode() is { } node ? TryGet(node) : null;

    internal static SakuraStandeeActionController? TryGet(NCreature node) =>
        node.Visuals.GetNodeOrNull<Node2D>("%Visuals")
            ?.GetNodeOrNull<SakuraStandeeActionController>(NodeName);

    internal static bool IsFourthActStandee(NCreature node) =>
        node.Entity.Monster is WindMonsterTemplate or DarkMonster;

    public override void _Ready()
    {
        TreeExiting += OnTreeExiting;
        if (!TestMode.IsOn)
            TaskHelper.RunSafely(PlayEntryAsync());
    }

    internal async Task PlayActionAsync(SakuraStandeeClip clip, Func<Task> resolveAtContact)
    {
        if (!TryBegin(SakuraStandeePlaybackPriority.Action, out var generation))
        {
            await resolveAtContact();
            return;
        }

        var stillCurrent = await PlayAnticipationAsync(clip, generation);
        if (stillCurrent)
        {
            if (!ApplyActionTexture(clip))
                SpawnAfterimage(clip);
            stillCurrent = await PlayContactAsync(clip, generation);
        }

        // Presentation interruption must never skip authoritative gameplay.
        await resolveAtContact();

        if (stillCurrent && IsCurrent(generation))
            await PlayRecoveryAsync(generation);
        FinishClip(generation);
    }

    internal void PlayHurt()
    {
        if (_playback.CanPlayNonDeath)
            TaskHelper.RunSafely(PlayHurtAsync());
    }

    internal bool PlayDeath()
    {
        if (!_playback.TryBeginDeath(out var generation))
            return false;

        CancelCurrent();
        StopIdle();
        TaskHelper.RunSafely(PlayDeathAsync(generation));
        return true;
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

        _body.Position = _restPosition + Vector2.Down * EntryOffsetY;
        _body.Rotation = 0f;
        _body.Scale = _restScale;
        _body.Modulate = new Color(1f, 1f, 1f, 0.86f);
        await PlayPhaseAsync(
            generation,
            EntryDuration,
            _restPosition,
            _restScale,
            0f,
            Colors.White,
            Tween.TransitionType.Cubic);
        FinishClip(generation);
    }

    private async Task PlayHurtAsync()
    {
        if (!TryBegin(SakuraStandeePlaybackPriority.Hurt, out var generation))
            return;

        ApplyRestTexture();
        var hurtColor = new Color(1f, 0.63f, 0.67f, 1f);
        if (!await PlayPhaseAsync(
                generation,
                0.07,
                _restPosition + Vector2.Right * 12f,
                _restScale * new Vector2(0.97f, 1.02f),
                0.018f,
                hurtColor,
                Tween.TransitionType.Quad))
            return;
        if (!await PlayPhaseAsync(
                generation,
                0.08,
                _restPosition + Vector2.Left * 5f,
                _restScale,
                -0.01f,
                Colors.White,
                Tween.TransitionType.Quad))
            return;
        await PlayRecoveryAsync(generation, 0.11);
        FinishClip(generation);
    }

    private async Task PlayDeathAsync(int generation)
    {
        ClearAfterimages();
        ApplyRestTexture();
        await PlayPhaseAsync(
            generation,
            DeathDuration,
            _restPosition + new Vector2(16f, 34f),
            _restScale * new Vector2(0.92f, 0.96f),
            0.11f,
            new Color(0.62f, 0.58f, 0.72f, 0.08f),
            Tween.TransitionType.Cubic,
            Tween.EaseType.In);
    }

    private Task<bool> PlayAnticipationAsync(SakuraStandeeClip clip, int generation) =>
        clip switch
        {
            SakuraStandeeClip.Attack => PlayPhaseAsync(
                generation, 0.11, _restPosition + Vector2.Right * 6f,
                _restScale * new Vector2(0.94f, 1.03f), 0.012f, Colors.White),
            SakuraStandeeClip.Cast => PlayPhaseAsync(
                generation, 0.16, _restPosition + Vector2.Down * 4f,
                _restScale * new Vector2(0.95f, 0.96f), 0f,
                new Color(0.74f, 0.9f, 1f, 0.88f)),
            SakuraStandeeClip.Buff => PlayPhaseAsync(
                generation, 0.14, _restPosition + Vector2.Down * 3f,
                _restScale * new Vector2(0.96f, 0.96f), 0f,
                new Color(1f, 0.91f, 0.68f, 0.9f)),
            SakuraStandeeClip.Summon => PlayPhaseAsync(
                generation, 0.18, _restPosition + Vector2.Down * 5f,
                _restScale * new Vector2(0.91f, 0.97f), 0f,
                new Color(0.83f, 0.78f, 1f, 0.86f)),
            _ => Task.FromResult(false)
        };

    private Task<bool> PlayContactAsync(SakuraStandeeClip clip, int generation) =>
        clip switch
        {
            SakuraStandeeClip.Attack => PlayPhaseAsync(
                generation, 0.08, _restPosition + Vector2.Left * 19f,
                _restScale * new Vector2(1.04f, 0.98f), -0.02f, Colors.White,
                Tween.TransitionType.Cubic, Tween.EaseType.Out),
            SakuraStandeeClip.Cast => PlayPhaseAsync(
                generation, 0.1, _restPosition + Vector2.Up * 7f,
                _restScale * new Vector2(1.045f, 1.045f), 0f, Colors.White,
                Tween.TransitionType.Back, Tween.EaseType.Out),
            SakuraStandeeClip.Buff => PlayPhaseAsync(
                generation, 0.11, _restPosition + Vector2.Up * 9f,
                _restScale * new Vector2(1.06f, 1.06f), 0f, Colors.White,
                Tween.TransitionType.Back, Tween.EaseType.Out),
            SakuraStandeeClip.Summon => PlayPhaseAsync(
                generation, 0.12, _restPosition + Vector2.Up * 5f,
                _restScale * new Vector2(1.075f, 1.04f), 0f, Colors.White,
                Tween.TransitionType.Back, Tween.EaseType.Out),
            _ => Task.FromResult(false)
        };

    private Task<bool> PlayRecoveryAsync(int generation, double duration = 0.18) =>
        PlayPhaseAsync(
            generation,
            duration,
            _restPosition,
            _restScale,
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

        _body.Position = _restPosition;
        _body.Scale = _restScale;
        _body.Rotation = 0f;
        _body.Modulate = Colors.White;
        ApplyRestTexture();
    }

    private void StartIdle()
    {
        if (!_playIdleMotion || !_playback.CanPlayNonDeath || !IsUsable(_body))
            return;

        StopIdle();
        var liftedPosition = _restPosition + Vector2.Up * IdleLift;
        var settledPosition = _restPosition + Vector2.Down * (IdleLift * 0.35f);
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

    private void ApplyRestTexture()
    {
        if (_sprite is not null
            && IsUsable(_sprite)
            && _textures.TryGetValue(_restTexturePath, out var texture))
            _sprite.Texture = texture;
    }

    private bool ApplyActionTexture(SakuraStandeeClip clip)
    {
        if (_sprite is null
            || !IsUsable(_sprite)
            || ActionTexturePath(clip) is not { } path
            || !_textures.TryGetValue(path, out var texture))
            return false;

        _sprite.Texture = texture;
        return true;
    }

    private string? ActionTexturePath(SakuraStandeeClip clip)
    {
        if (_restTexturePath == WindEnemyAssets.Illusion
            && clip is SakuraStandeeClip.Attack or SakuraStandeeClip.Summon)
            return WindEnemyAssets.IllusionCast;
        if (_restTexturePath == WindEnemyAssets.Windy
            && clip is SakuraStandeeClip.Attack or SakuraStandeeClip.Summon)
            return WindEnemyAssets.WindyAction;
        if (_restTexturePath == WindEnemyAssets.Dash && clip == SakuraStandeeClip.Attack)
            return WindEnemyAssets.DashAttack;
        if (_restTexturePath == WindEnemyAssets.Sleep && clip == SakuraStandeeClip.Cast)
            return WindEnemyAssets.SleepCast;
        if (_restTexturePath == DarkEnemyAssets.Standee
            && clip is SakuraStandeeClip.Attack or SakuraStandeeClip.Cast)
            return DarkEnemyAssets.Action;
        return null;
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

    private void SpawnAfterimage(SakuraStandeeClip clip)
    {
        if (_sprite is null || !IsUsable(_sprite) || _sprite.GetParent() is not { } parent)
            return;

        var tint = clip switch
        {
            SakuraStandeeClip.Cast => new Color(0.57f, 0.82f, 1f, 0.34f),
            SakuraStandeeClip.Buff => new Color(1f, 0.82f, 0.38f, 0.32f),
            SakuraStandeeClip.Summon => new Color(0.76f, 0.57f, 1f, 0.32f),
            _ => new Color(0.72f, 0.9f, 1f, 0.3f)
        };
        var drift = clip == SakuraStandeeClip.Attack
            ? Vector2.Right * 13f
            : Vector2.Down * 8f;
        var afterimage = new Sprite2D
        {
            Texture = _sprite.Texture,
            Centered = _sprite.Centered,
            Offset = _sprite.Offset,
            FlipH = _sprite.FlipH,
            FlipV = _sprite.FlipV,
            Position = _sprite.Position,
            Rotation = _sprite.Rotation,
            Scale = _sprite.Scale,
            Modulate = tint,
            ZIndex = _sprite.ZIndex - 1,
            ZAsRelative = _sprite.ZAsRelative
        };
        parent.AddChild(afterimage);
        _afterimages.Add(afterimage);

        var fade = afterimage.CreateTween().SetParallel();
        fade.TweenProperty(afterimage, "position", afterimage.Position + drift, 0.18)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        fade.TweenProperty(afterimage, "modulate:a", 0f, 0.18)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);
        fade.Chain().TweenCallback(Callable.From(() => RemoveAfterimage(afterimage)));
    }

    private void RemoveAfterimage(Sprite2D afterimage)
    {
        _afterimages.Remove(afterimage);
        if (GodotObject.IsInstanceValid(afterimage))
            afterimage.QueueFree();
    }

    private void ClearAfterimages()
    {
        foreach (var afterimage in _afterimages.ToArray())
            RemoveAfterimage(afterimage);
    }

    private void OnTreeExiting()
    {
        _playback.Dispose();
        TreeExiting -= OnTreeExiting;
        CancelCurrent();
        StopIdle();
        ClearAfterimages();
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

[HarmonyLib.HarmonyPatch(typeof(NCreature), nameof(NCreature.SetAnimationTrigger))]
internal static class SakuraStandeeHitPatch
{
    private static void Postfix(NCreature __instance, string trigger)
    {
        if (trigger == "Hit"
            && __instance.Entity.Monster is not SakuraMod.SakuraModCode.FourthAct.Wind.Models.IllusionProjectionMonster
            && SakuraStandeeActionController.IsFourthActStandee(__instance))
        {
            SakuraStandeeActionController.TryGet(__instance)?.PlayHurt();
        }
    }
}

[HarmonyLib.HarmonyPatch(typeof(NCreature), nameof(NCreature.StartDeathAnim))]
internal static class SakuraStandeeDeathPatch
{
    private static void Prefix(NCreature __instance)
    {
        if (__instance.Entity.Monster is SakuraMod.SakuraModCode.FourthAct.Wind.Models.IllusionProjectionMonster)
            return;
        if (!SakuraStandeeActionController.IsFourthActStandee(__instance)
            || SakuraStandeeActionController.TryGet(__instance) is not { } controller
            || !controller.PlayDeath())
            return;

        if (__instance.Entity.Monster is { } monster && monster.HasDeathSfx)
            SfxCmd.PlayDeath(monster);
    }
}
