using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.TestSupport;
using SakuraMod.SakuraModCode.Extensions;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.SakuraModCode.Relics;

namespace SakuraMod.SakuraModCode.Character;

internal readonly record struct SakuraKeroCompanionLayout(Vector2 Offset, float Scale);

internal static class SakuraKeroCombatCompanion
{
    internal const string CompanionNodeName = "SakuraKeroCombatCompanion";
    internal const string TextureFile = "charui/combat/kero_companion.png";

    private const float IdleLift = 4.5f;
    private const float IdleDrop = 1.5f;
    private const float IdleHalfDuration = 3.0f;
    private const float ReactionHopHeight = 18f;
    private const float ReactionRiseDuration = 0.14f;
    private const float ReactionSettleDuration = 0.22f;
    private const float ReactionGlowAlpha = 0.62f;

    internal static readonly SakuraKeroCompanionLayout StandardLayout =
        new(new Vector2(-190f, -20f), 0.22f);
    internal static readonly SakuraKeroCompanionLayout ChibiLayout =
        new(new Vector2(-155f, -25f), 0.18f);

    internal static string TexturePath => TextureFile.ImagePath();

    internal static bool ShouldMount(Player player) =>
        ShouldMount(
            player.Character is ClassicSakura,
            player.GetRelic<ClassicCerberusRelic>() is not null,
            player.GetRelic<ClassicUltimateWandRelic>() is not null);

    internal static bool ShouldMount(bool isSakura, bool hasCerberus, bool hasUltimateWand) =>
        isSakura && (hasCerberus || hasUltimateWand);

    internal static SakuraKeroCompanionLayout SelectLayout(bool useChibi) =>
        useChibi ? ChibiLayout : StandardLayout;

    public static void Mount(NCreature creatureNode)
    {
        if (TestMode.IsOn
            || creatureNode.Entity.Player is not { } player
            || player.Creature.CombatState is not { } combatState
            || !ShouldMount(player))
        {
            return;
        }

        var anchor = creatureNode.Visuals.VfxSpawnPosition;
        if (!GodotObject.IsInstanceValid(anchor))
            throw new InvalidOperationException("Cannot mount Kero without Sakura's local VFX anchor.");
        if (anchor.GetNodeOrNull<Node2D>(CompanionNodeName) is not null)
            return;

        var texture = ResourceLoader.Load<Texture2D>(TexturePath, null, ResourceLoader.CacheMode.Reuse)
            ?? throw new InvalidOperationException($"Could not load Kero combat texture: {TexturePath}");
        var layout = SelectLayout(SakuraCombatArtPreference.IsChibi(player));

        var root = new Node2D
        {
            Name = CompanionNodeName,
            Position = layout.Offset,
            Scale = Vector2.One * layout.Scale,
            ZAsRelative = true,
            ZIndex = 1
        };
        var idleRoot = new Node2D { Name = "IdleRoot" };
        var reactionRoot = new Node2D { Name = "ReactionRoot" };
        var glow = new Sprite2D
        {
            Name = "Glow",
            Texture = texture,
            Centered = true,
            Scale = Vector2.One * 1.06f,
            Modulate = new Color(1f, 0.68f, 0.24f, 0f),
            Material = new CanvasItemMaterial
            {
                BlendMode = CanvasItemMaterial.BlendModeEnum.Add
            }
        };
        var kero = new Sprite2D
        {
            Name = "Kero",
            Texture = texture,
            Centered = true
        };

        reactionRoot.AddChild(glow);
        reactionRoot.AddChild(kero);
        idleRoot.AddChild(reactionRoot);
        root.AddChild(idleRoot);
        anchor.AddChildSafely(root);

        var state = new CompanionState(
            root,
            idleRoot,
            reactionRoot,
            glow,
            kero,
            player,
            creatureNode,
            combatState);
        state.Start();
    }

    private sealed class CompanionState : IDisposable
    {
        private readonly Node2D _root;
        private readonly Node2D _idleRoot;
        private readonly Node2D _reactionRoot;
        private readonly Sprite2D _glow;
        private readonly Sprite2D _kero;
        private readonly Player _player;
        private readonly Creature _creature;
        private readonly NCreature _creatureNode;
        private readonly ICombatState _combatState;
        private Tween? _idleTween;
        private Tween? _reactionTween;
        private bool _disposed;

        public CompanionState(
            Node2D root,
            Node2D idleRoot,
            Node2D reactionRoot,
            Sprite2D glow,
            Sprite2D kero,
            Player player,
            NCreature creatureNode,
            ICombatState combatState)
        {
            _root = root;
            _idleRoot = idleRoot;
            _reactionRoot = reactionRoot;
            _glow = glow;
            _kero = kero;
            _player = player;
            _creature = player.Creature;
            _creatureNode = creatureNode;
            _combatState = combatState;
        }

        public void Start()
        {
            ClassicCerberusMarkPower.MarkApplied += OnMarkApplied;
            _creature.Died += OnCreatureDied;
            CombatManager.Instance.CombatEnded += OnCombatEnded;
            _root.TreeExiting += OnTreeExiting;
            StartIdle();
        }

        private void StartIdle()
        {
            _idleRoot.Position = Vector2.Zero;
            _idleTween = _idleRoot.CreateTween().SetLoops();
            _idleTween.TweenProperty(
                    _idleRoot,
                    "position",
                    Vector2.Up * IdleLift,
                    IdleHalfDuration)
                .SetEase(Tween.EaseType.InOut)
                .SetTrans(Tween.TransitionType.Sine);
            _idleTween.TweenProperty(
                    _idleRoot,
                    "position",
                    Vector2.Down * IdleDrop,
                    IdleHalfDuration)
                .SetEase(Tween.EaseType.InOut)
                .SetTrans(Tween.TransitionType.Sine);
        }

        private void OnMarkApplied(Creature applier)
        {
            if (_disposed || !ReferenceEquals(applier, _creature))
                return;
            if (!IsCurrentMount())
            {
                DisposeAndFree();
                return;
            }

            StartReaction();
        }

        private void StartReaction()
        {
            KillTween(ref _reactionTween);
            ResetReaction();

            var tween = _reactionRoot.CreateTween();
            _reactionTween = tween;
            tween.TweenProperty(
                    _reactionRoot,
                    "position",
                    Vector2.Up * ReactionHopHeight,
                    ReactionRiseDuration)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Cubic);
            tween.Parallel().TweenProperty(
                    _glow,
                    "modulate:a",
                    ReactionGlowAlpha,
                    ReactionRiseDuration)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Quad);
            tween.Parallel().TweenProperty(
                    _kero,
                    "modulate",
                    new Color(1f, 0.91f, 0.66f, 1f),
                    ReactionRiseDuration)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Quad);
            tween.TweenProperty(
                    _reactionRoot,
                    "position",
                    Vector2.Zero,
                    ReactionSettleDuration)
                .SetEase(Tween.EaseType.In)
                .SetTrans(Tween.TransitionType.Cubic);
            tween.Parallel().TweenProperty(
                    _glow,
                    "modulate:a",
                    0f,
                    ReactionSettleDuration)
                .SetEase(Tween.EaseType.In)
                .SetTrans(Tween.TransitionType.Quad);
            tween.Parallel().TweenProperty(
                    _kero,
                    "modulate",
                    Colors.White,
                    ReactionSettleDuration)
                .SetEase(Tween.EaseType.In)
                .SetTrans(Tween.TransitionType.Quad);
            tween.TweenCallback(Callable.From(() => FinishReaction(tween)));
        }

        private void FinishReaction(Tween tween)
        {
            if (_disposed || !ReferenceEquals(_reactionTween, tween))
                return;

            _reactionTween = null;
            ResetReaction();
        }

        private void ResetReaction()
        {
            if (!GodotObject.IsInstanceValid(_reactionRoot))
                return;

            _reactionRoot.Position = Vector2.Zero;
            _glow.Modulate = new Color(1f, 0.68f, 0.24f, 0f);
            _kero.Modulate = Colors.White;
        }

        private bool IsCurrentMount() =>
            GodotObject.IsInstanceValid(_root)
            && _root.IsInsideTree()
            && GodotObject.IsInstanceValid(_creatureNode)
            && _creatureNode.IsInsideTree()
            && ReferenceEquals(_creatureNode.Entity.Player, _player)
            && ReferenceEquals(_creature.CombatState, _combatState);

        private void OnCreatureDied(Creature creature)
        {
            if (ReferenceEquals(creature, _creature))
                DisposeAndFree();
        }

        private void OnCombatEnded(CombatRoom _) =>
            DisposeAndFree();

        private void OnTreeExiting() =>
            Dispose();

        private void DisposeAndFree()
        {
            Dispose();
            if (GodotObject.IsInstanceValid(_root) && !_root.IsQueuedForDeletion())
                _root.QueueFreeSafely();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            ClassicCerberusMarkPower.MarkApplied -= OnMarkApplied;
            _creature.Died -= OnCreatureDied;
            CombatManager.Instance.CombatEnded -= OnCombatEnded;
            if (GodotObject.IsInstanceValid(_root))
                _root.TreeExiting -= OnTreeExiting;
            KillTween(ref _idleTween);
            KillTween(ref _reactionTween);
        }

        private static void KillTween(ref Tween? tween)
        {
            if (tween is { } current
                && GodotObject.IsInstanceValid(current)
                && current.IsValid())
            {
                current.Kill();
            }
            tween = null;
        }
    }
}
