using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.TestSupport;
using SakuraMod.SakuraModCode.Cards;

namespace SakuraMod.SakuraModCode.Classic.Cards;

internal sealed partial class ClassicTurnCardPreviewContainer : Control;

internal static class ClassicTurnTransformationTimeline
{
    public const float EnlargeEnd = 0.25f;
    public const float SwitchStart = 1.95f;
    public const float RevealStart = 3.95f;
    public const float ShrinkStart = 4.45f;
    public const float TotalDuration = 4.95f;
    public const float GatherDuration = 2f;
    public const float SwitchDuration = 2f;
    public const float DiffusionDuration = 3f;
    public const float LoweredPitchScale = 0.4f;
    public const int GatherParticleCount = 256;
    public const int DiffusionParticleCount = 48;
    public const int LuminStripCount = 72;

    public static readonly string[] CompletionAudioPaths =
    [
        ClassicTurnTransformationVfx.AudioPath("SOTE_SFX_Buff_1_v1.ogg"),
        ClassicTurnTransformationVfx.AudioPath("SOTE_SFX_Buff_2_v1.ogg"),
        ClassicTurnTransformationVfx.AudioPath("SOTE_SFX_Buff_3_v1.ogg")
    ];
}

internal static class ClassicTurnTransformationVfx
{
    internal const string ScenePath = MainFile.ResPath + "/scenes/combat/classic_turn_transformation_vfx.tscn";
    internal const string LuminPath = MainFile.ResPath + "/images/vfx/classic_turn_lumin.png";
    internal const string SfxRoot = MainFile.ResPath + "/sfx/classic_turn";
    internal static readonly string TurnAudioPath = AudioPath("SOTE_SFX_PlayerTurn_v4_1.ogg");
    internal static readonly string OpeningBuffAudioPath = AudioPath("SOTE_SFX_Buff_2_v1.ogg");
    internal static readonly string SwitchAudioPath = AudioPath("STS_SFX_Guardian3Destroy_v2.ogg");

    private static readonly Vector2 CardSize = SakuraCardGeometry.ClassicLayoutSize;
    private static readonly Color ParticleColor = new(0.73f, 0.275f, 0.965f, 0.8f);
    private static readonly Color LuminColor = new(0.969f, 0.482f, 0.988f, 1f);

    internal static string AudioPath(string fileName) => $"{SfxRoot}/{fileName}";

    public static Session? TryCreate(CardModel startCard)
    {
        if (TestMode.IsOn || NCombatRoom.Instance?.Ui is not { } ui)
            return null;

        var scene = ResourceLoader.Load<PackedScene>(ScenePath, null, ResourceLoader.CacheMode.Reuse)
            ?? throw new InvalidOperationException($"Could not load Classic Turn VFX scene: {ScenePath}");
        var root = scene.Instantiate<Control>();
        root.ZAsRelative = false;
        root.ZIndex = 4000;
        ui.AddChildSafely(root);
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        try
        {
            return new Session(root, startCard);
        }
        catch
        {
            root.QueueFreeSafely();
            throw;
        }
    }

    internal sealed class Session : IDisposable
    {
        private readonly Control _root;
        private readonly Control _center;
        private readonly Control _oldClip;
        private readonly Control _newCardContainer;
        private readonly TextureRect _mist;
        private readonly ColorRect _borderFlash;
        private readonly Node2D _gatherAnchor;
        private readonly Node2D _diffusionAnchor;
        private readonly Node2D _luminAnchor;
        private readonly AudioStreamPlayer _primaryAudio;
        private readonly AudioStreamPlayer _accentAudio;
        private readonly List<Tween> _tweens = [];
        private readonly List<Sprite2D> _luminStrips = [];
        private readonly RandomNumberGenerator _random = new();
        private readonly NCard _oldCard;
        private bool _interrupted;
        private bool _disposed;

        public Session(Control root, CardModel startCard)
        {
            _root = root;
            _center = root.GetNode<Control>("%Center");
            _oldClip = root.GetNode<Control>("%OldClip");
            _newCardContainer = root.GetNode<Control>("%NewCard");
            _mist = root.GetNode<TextureRect>("%Mist");
            _borderFlash = root.GetNode<ColorRect>("%BorderFlash");
            _gatherAnchor = root.GetNode<Node2D>("%GatherParticles");
            _diffusionAnchor = root.GetNode<Node2D>("%DiffusionParticles");
            _luminAnchor = root.GetNode<Node2D>("%Lumin");
            _primaryAudio = root.GetNode<AudioStreamPlayer>("%PrimaryAudio");
            _accentAudio = root.GetNode<AudioStreamPlayer>("%AccentAudio");
            _oldCard = CreatePreview(_oldClip, startCard);
            _center.Scale = Vector2.One * 0.666f;
            _random.Randomize();
            BuildLuminStrips();

            CombatManager.Instance.CombatEnded += OnCombatEnded;
            _root.TreeExiting += OnTreeExiting;
        }

        public async Task<bool> PlayPrelude()
        {
            if (!IsActive())
                return false;

            var enlargeTween = Track(_center.CreateTween());
            enlargeTween.TweenProperty(_center, "scale", Vector2.One, ClassicTurnTransformationTimeline.EnlargeEnd)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Cubic);
            if (!await WaitActive(ClassicTurnTransformationTimeline.EnlargeEnd))
                return false;

            PlayAudio(
                _primaryAudio,
                TurnAudioPath,
                ClassicTurnTransformationTimeline.LoweredPitchScale);
            PlayAudio(
                _accentAudio,
                OpeningBuffAudioPath,
                ClassicTurnTransformationTimeline.LoweredPitchScale);
            FlashBorder();
            TaskHelper.RunSafely(AnimateGatherParticles());

            return await WaitActive(
                ClassicTurnTransformationTimeline.SwitchStart
                - ClassicTurnTransformationTimeline.EnlargeEnd);
        }

        public async Task PlayReveal(CardModel endCard)
        {
            if (!IsActive())
                return;

            CreatePreview(_newCardContainer, endCard);
            PlayAudio(
                _primaryAudio,
                SwitchAudioPath,
                1f);

            var switchTween = _root.CreateTween();
            Track(switchTween);
            switchTween.TweenMethod(
                    Callable.From<float>(UpdateSwitch),
                    0f,
                    1f,
                    ClassicTurnTransformationTimeline.SwitchDuration)
                .SetEase(Tween.EaseType.InOut)
                .SetTrans(Tween.TransitionType.Sine);
            if (!await WaitActive(ClassicTurnTransformationTimeline.SwitchDuration))
                return;

            _oldCard.Visible = false;
            _mist.Visible = false;
            PlayAudio(
                _primaryAudio,
                ClassicTurnTransformationTimeline.CompletionAudioPaths[
                    _random.RandiRange(0, ClassicTurnTransformationTimeline.CompletionAudioPaths.Length - 1)],
                1f);
            SpawnDiffusionTail();

            if (!await WaitActive(
                    ClassicTurnTransformationTimeline.ShrinkStart
                    - ClassicTurnTransformationTimeline.RevealStart))
            {
                return;
            }

            var shrinkTween = Track(_center.CreateTween());
            shrinkTween.TweenProperty(_center, "scale", Vector2.One * 0.666f, 0.25f)
                .SetEase(Tween.EaseType.In)
                .SetTrans(Tween.TransitionType.Cubic);
            await WaitActive(ClassicTurnTransformationTimeline.TotalDuration
                             - ClassicTurnTransformationTimeline.ShrinkStart);
        }

        private static NCard CreatePreview(Control container, CardModel card)
        {
            var origin = new ClassicTurnCardPreviewContainer
            {
                Position = CardSize * 0.5f,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            container.AddChildSafely(origin);

            var preview = NCard.Create(card)
                ?? throw new InvalidOperationException($"Could not create Classic Turn preview for {card.Id}.");
            origin.AddChildSafely(preview);
            preview.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
            preview.Position = Vector2.Zero;
            preview.MouseFilter = Control.MouseFilterEnum.Ignore;
            return preview;
        }

        private void FlashBorder()
        {
            _borderFlash.Color = new Color(0.73f, 0.275f, 0.965f, 0.34f);
            var flashTween = Track(_borderFlash.CreateTween());
            flashTween.TweenProperty(_borderFlash, "color:a", 0f, 0.8f)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Quad);
        }

        private void BuildLuminStrips()
        {
            var texture = ResourceLoader.Load<Texture2D>(LuminPath, null, ResourceLoader.CacheMode.Reuse)
                ?? throw new InvalidOperationException($"Could not load Classic Turn lumin texture: {LuminPath}");
            var material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
            for (var index = 0; index < ClassicTurnTransformationTimeline.LuminStripCount; index++)
            {
                var proportion = index / (float)(ClassicTurnTransformationTimeline.LuminStripCount - 1);
                var strip = new Sprite2D
                {
                    Texture = texture,
                    Centered = true,
                    Position = new Vector2(CardSize.X * proportion, 0f),
                    Scale = new Vector2(0.55f, _random.RandfRange(0.45f, 1.35f)),
                    Modulate = new Color(LuminColor, 0f),
                    Material = material
                };
                _luminAnchor.AddChildSafely(strip);
                _luminStrips.Add(strip);
            }
        }

        private void UpdateSwitch(float progress)
        {
            if (!IsActive())
                return;

            var remainingHeight = CardSize.Y * (1f - progress);
            _oldClip.Size = new Vector2(CardSize.X, remainingHeight);
            var boundary = remainingHeight;
            _luminAnchor.Position = new Vector2(0f, boundary);
            var glowAlpha = Mathf.Sin(progress * Mathf.Pi);
            _mist.SelfModulate = new Color(0.969f, 0.482f, 0.988f, glowAlpha * 0.72f);
            foreach (var strip in _luminStrips)
            {
                var wave = 0.65f + 0.35f * Mathf.Sin(progress * 18f + strip.Position.X * 0.09f);
                strip.Modulate = new Color(LuminColor, glowAlpha * wave);
            }
        }

        private async Task AnimateGatherParticles()
        {
            if (!IsActive())
                return;

            var texture = ResourceLoader.Load<Texture2D>(LuminPath, null, ResourceLoader.CacheMode.Reuse);
            if (texture is null)
                return;

            var particles = CreateParticleMesh(
                _gatherAnchor,
                texture,
                ClassicTurnTransformationTimeline.GatherParticleCount);
            var starts = new Vector2[ClassicTurnTransformationTimeline.GatherParticleCount];
            for (var index = 0; index < starts.Length; index++)
            {
                var angle = _random.RandfRange(0f, Mathf.Tau);
                var radius = _random.RandfRange(280f, 760f);
                starts[index] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }

            var elapsed = 0f;
            while (elapsed < ClassicTurnTransformationTimeline.GatherDuration && IsActive())
            {
                var progress = Mathf.Clamp(
                    elapsed / ClassicTurnTransformationTimeline.GatherDuration,
                    0f,
                    1f);
                var eased = 1f - Mathf.Pow(1f - progress, 3f);
                for (var index = 0; index < starts.Length; index++)
                {
                    var scale = 0.16f + 0.2f * (1f - progress);
                    particles.Multimesh.SetInstanceTransform2D(
                        index,
                        new Transform2D(0f, Vector2.One * scale, 0f, starts[index].Lerp(Vector2.Zero, eased)));
                }
                elapsed += await _root.AwaitProcessFrame();
            }

            particles.QueueFreeSafely();
        }

        private void SpawnDiffusionTail()
        {
            if (NCombatRoom.Instance?.CombatVfxContainer is not { } container)
                return;

            var tail = new Node2D
            {
                Name = "ClassicTurnDiffusionTail",
                ZAsRelative = false,
                ZIndex = 4001
            };
            container.AddChildSafely(tail);
            tail.GlobalPosition = _diffusionAnchor.GlobalPosition;
            TaskHelper.RunSafely(AnimateDiffusionTail(tail));
        }

        private async Task AnimateDiffusionTail(Node2D tail)
        {
            var texture = ResourceLoader.Load<Texture2D>(LuminPath, null, ResourceLoader.CacheMode.Reuse);
            if (texture is null || !tail.IsInsideTree())
            {
                tail.QueueFreeSafely();
                return;
            }

            var particles = CreateParticleMesh(
                tail,
                texture,
                ClassicTurnTransformationTimeline.DiffusionParticleCount);
            var directions = new Vector2[ClassicTurnTransformationTimeline.DiffusionParticleCount];
            var speeds = new float[directions.Length];
            for (var index = 0; index < directions.Length; index++)
            {
                var angle = _random.RandfRange(0f, Mathf.Tau);
                directions[index] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                speeds[index] = _random.RandfRange(120f, 260f);
            }

            var elapsed = 0f;
            while (elapsed < ClassicTurnTransformationTimeline.DiffusionDuration
                   && GodotObject.IsInstanceValid(tail)
                   && tail.IsInsideTree())
            {
                var progress = Mathf.Clamp(
                    elapsed / ClassicTurnTransformationTimeline.DiffusionDuration,
                    0f,
                    1f);
                for (var index = 0; index < directions.Length; index++)
                {
                    var distance = speeds[index] * elapsed * (1f - progress * 0.35f);
                    var scale = Mathf.Max(0f, 0.32f * (1f - progress));
                    particles.Multimesh.SetInstanceTransform2D(
                        index,
                        new Transform2D(0f, Vector2.One * scale, 0f, directions[index] * distance));
                }
                elapsed += await tail.AwaitProcessFrame();
            }

            tail.QueueFreeSafely();
        }

        private static MultiMeshInstance2D CreateParticleMesh(Node parent, Texture2D texture, int count)
        {
            var multiMesh = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform2D,
                UseColors = true,
                InstanceCount = count
            };
            var particles = new MultiMeshInstance2D
            {
                Multimesh = multiMesh,
                Texture = texture,
                Modulate = ParticleColor,
                Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add }
            };
            parent.AddChildSafely(particles);
            return particles;
        }

        private static void PlayAudio(AudioStreamPlayer player, string path, float pitchScale)
        {
            var stream = ResourceLoader.Load<AudioStream>(path, null, ResourceLoader.CacheMode.Reuse)
                ?? throw new InvalidOperationException($"Could not load Classic Turn audio: {path}");
            player.Stop();
            player.Stream = stream;
            player.PitchScale = pitchScale;
            player.Play();
        }

        private async Task<bool> WaitActive(float seconds)
        {
            var elapsed = 0f;
            while (elapsed < seconds)
            {
                if (!IsActive())
                    return false;
                elapsed += await _root.AwaitProcessFrame();
            }
            return IsActive();
        }

        private Tween Track(Tween tween)
        {
            _tweens.Add(tween);
            return tween;
        }

        private bool IsActive() =>
            !_disposed
            && !_interrupted
            && !CombatManager.Instance.IsEnding
            && GodotObject.IsInstanceValid(_root)
            && _root.IsInsideTree()
            && !_root.IsQueuedForDeletion();

        private void OnCombatEnded(CombatRoom _) =>
            _interrupted = true;

        private void OnTreeExiting() =>
            _interrupted = true;

        public void Dispose()
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
            if (GodotObject.IsInstanceValid(_primaryAudio))
                _primaryAudio.Stop();
            if (GodotObject.IsInstanceValid(_accentAudio))
                _accentAudio.Stop();
            if (GodotObject.IsInstanceValid(_root) && !_root.IsQueuedForDeletion())
                _root.QueueFreeSafely();
        }
    }
}
