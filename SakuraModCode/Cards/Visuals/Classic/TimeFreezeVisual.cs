using System.Runtime.CompilerServices;
using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.TestSupport;
using SakuraMod.SakuraModCode.Powers;

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>
/// Holds a creature still while it has Sakura Time's Time Stop: its Spine animation
/// is paused, an amber tint settles over it, and a small hourglass beside it shows
/// the remaining turns as sand.
/// </summary>
internal static partial class TimeFreezeVisual
{
    internal const string ShaderPath = MainFile.ResPath + "/shaders/card_vfx/time_freeze.gdshader";
    internal const string RootName = "SakuraTimeFreeze";
    internal static IReadOnlyList<string> AssetPaths { get; } = [ShaderPath];
    private const float IntroSeconds = 0.45f;
    private const float ReleaseSeconds = 0.40f;
    private const float DropSeconds = 0.35f;
    private const float ShakeSeconds = 0.24f;
    private const float TintFadeSeconds = 0.15f;
    // A crack fades to this and stays until the hold ends.
    private const float CrackHold = 0.6f;
    private const string TintSource = "time";
    // Multiplies the creature's own RGB: warms it toward the card's gold sand.
    private static readonly Vector3 AmberTint = new(1.10f, 0.92f, 0.68f);
    private static readonly ConditionalWeakTable<Creature, FreezeRoot> Roots = [];

    internal static void Mount(Creature creature)
    {
        if (TestMode.IsOn || !SakuraModConfig.IsCardVfxEnabled()
            || !creature.IsAlive || creature.GetPowerAmount<ClassicTimePower>() <= 0)
            return;
        if (Roots.TryGetValue(creature, out var existing))
        {
            if (GodotObject.IsInstanceValid(existing) && !existing.IsQueuedForDeletion())
            {
                existing.Refresh();
                return;
            }
            Roots.Remove(creature);
        }

        if (NCombatRoom.Instance is not { } room
            || room.GetCreatureNode(creature) is not { } node
            || !GodotObject.IsInstanceValid(node.Visuals)
            || CelVfxGeometry.ResolveCaster(node) is null)
            return;

        FreezeRoot? root = null;
        try
        {
            var material = new ShaderMaterial { Shader = PreloadManager.Cache.GetAsset<Shader>(ShaderPath) };
            material.SetShaderParameter("sand", 1f);
            material.SetShaderParameter("pour", 0f);
            material.SetShaderParameter("intro", 0f);
            material.SetShaderParameter("release", 0f);
            material.SetShaderParameter("shake", 0f);
            material.SetShaderParameter("crack", 0f);
            root = new FreezeRoot
            {
                Name = $"{RootName}_{creature.CombatId}",
                ZAsRelative = true,
                ZIndex = 0
            };
            var canvas = new ColorRect
            {
                Name = "Hourglass",
                Color = Colors.White,
                Material = material,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            root.AddChild(canvas);
            // Directly after the body, like the Freeze shell: the creature's own
            // health bar, Block, Powers, intents and reticle draw over it.
            node.AddChildSafely(root);
            node.MoveChildSafely(root, node.Visuals.GetIndex() + 1);
            Roots.Add(creature, root);
            root.Start(creature, node, canvas, material);
        }
        catch (Exception exception)
        {
            root?.Retire();
            MainFile.Logger.Error($"Could not mount Time freeze: {exception}");
        }
    }

    /// <param name="unblocked">The hit took HP, so native also queued its hurt animation.</param>
    internal static void NotifyHit(Creature creature, bool unblocked)
    {
        if (!TestMode.IsOn && Roots.TryGetValue(creature, out var root)
            && GodotObject.IsInstanceValid(root) && !root.IsQueuedForDeletion())
            root.FlashHit(unblocked);
    }

    private sealed partial class FreezeRoot : Node2D
    {
        private Creature? _creature;
        private NCreature _node = null!;
        private ICombatState? _combat;
        private ColorRect _canvas = null!;
        private ShaderMaterial _material = null!;
        private Vector2 _size;
        private float _peak;
        private float _sand = 1f;
        private float _pour;
        private float _intro;
        private float _release;
        private float _shake;
        private float _crack;
        private float _tint;
        private bool _releasing;
        private bool _spineHeld;
        private bool _hurtQueued;
        private bool _retired;

        internal void Start(Creature creature, NCreature node, ColorRect canvas, ShaderMaterial material)
        {
            _creature = creature;
            _node = node;
            _combat = creature.CombatState;
            _canvas = canvas;
            _material = material;
            // Died fires before native starts the death animation, so the body is
            // released in time for it to play.
            creature.Died += OnDied;
            node.TreeExiting += Retire;
            CombatManager.Instance.CombatEnded += OnCombatEnded;
            FollowBody();
            HoldSpine();
        }

        /// <summary>Applied or stacked again; a reapplication during release holds the creature again.</summary>
        internal void Refresh()
        {
            if (_retired || !_releasing)
                return;
            _releasing = false;
            _release = 0f;
            _intro = 0f;
            _peak = 0f;
            HoldSpine();
        }

        internal void FlashHit(bool unblocked)
        {
            _shake = 1f;
            if (unblocked)
            {
                _crack = 1f;
                _hurtQueued |= _spineHeld;
            }
            // Written now, like the Freeze shell's hit, so the hit frame shows it.
            _material.SetShaderParameter("shake", _shake);
            _material.SetShaderParameter("crack", _crack);
        }

        public override void _Process(double delta)
        {
            if (_creature is null || _retired)
                return;
            try
            {
                if (!SakuraModConfig.IsCardVfxEnabled() || !_creature.IsAlive
                    || !GodotObject.IsInstanceValid(_node) || !_node.IsInsideTree()
                    || !ReferenceEquals(_creature.CombatState, _combat))
                {
                    Retire();
                    return;
                }

                var dt = Math.Max(0f, (float)delta);
                // Read authoritative state each frame; no gameplay flag or timer is copied.
                var amount = (float)_creature.GetPowerAmount<ClassicTimePower>();
                float target;
                if (amount > 0f)
                {
                    Refresh();
                    _peak = Math.Max(_peak, amount);
                    target = amount / _peak;
                }
                else
                {
                    if (!_releasing)
                    {
                        _releasing = true;
                        ReleaseSpine();
                    }
                    target = 0f;
                }

                _intro = Math.Min(1f, _intro + dt / IntroSeconds);
                if (_releasing)
                {
                    _release = Math.Min(1f, _release + dt / ReleaseSeconds);
                    if (_release >= 1f)
                    {
                        Retire();
                        return;
                    }
                }

                // Stacking refills at once; a decrement lets the sand fall.
                _sand = target > _sand ? target : Mathf.MoveToward(_sand, target, dt / DropSeconds);
                _pour = _sand > target + 0.001f ? 1f : Mathf.MoveToward(_pour, 0f, dt / 0.1f);
                _shake = Math.Max(0f, _shake - dt / ShakeSeconds);
                if (_crack > CrackHold)
                    _crack = Math.Max(CrackHold, _crack - dt / ShakeSeconds);

                _tint = Mathf.MoveToward(_tint, _releasing ? 0f : 1f, dt / TintFadeSeconds);
                if (_tint > 0f)
                    CreatureTint.Set(_node, TintSource, Vector3.One.Lerp(AmberTint, _tint));
                else
                    CreatureTint.Clear(_node, TintSource);

                FollowBody();
                _material.SetShaderParameter("sand", _sand);
                _material.SetShaderParameter("pour", _pour);
                _material.SetShaderParameter("intro", _intro);
                _material.SetShaderParameter("release", _release);
                _material.SetShaderParameter("shake", _shake);
                _material.SetShaderParameter("crack", _crack);
            }
            catch (Exception exception)
            {
                Retire();
                MainFile.Logger.Error($"Could not update Time freeze: {exception}");
            }
        }

        // Only the AnimationState scale is written: native writes per-track scales
        // (idle offsets, Doom) and nothing native writes this one for creatures.
        private void HoldSpine()
        {
            if (_spineHeld || !GodotObject.IsInstanceValid(_node)
                || !_node.HasSpineAnimation || !_node.SpineAnimation.IsValid)
                return;
            _node.SpineAnimation.SetTimeScale(0f);
            _spineHeld = true;
        }

        private void ReleaseSpine()
        {
            if (!_spineHeld)
                return;
            _spineHeld = false;
            if (!GodotObject.IsInstanceValid(_node) || !_node.SpineAnimation.IsValid)
                return;
            // A hit while held queued native's hurt state; go back to idle instead
            // of playing it late.
            if (_hurtQueued && _creature is { IsAlive: true })
                _node.SetAnimationTrigger("Idle");
            _hurtQueued = false;
            _node.SpineAnimation.SetTimeScale(1f);
        }

        private void FollowBody()
        {
            if (CelVfxGeometry.ResolveCaster(_node) is not { } anchor)
                return;
            var size = anchor.BodySize;
            GlobalPosition = new Vector2(anchor.BodyCenter.X, anchor.Floor.Y - size.Y * 0.5f);
            if (size == _size)
                return;
            _size = size;
            // Side margin holds the hourglass and its click ring beside the body;
            // vertical margin holds the falling motes.
            var canvas = size + new Vector2(340f, 200f);
            _canvas.Size = canvas;
            _canvas.Position = -canvas * 0.5f;
            _material.SetShaderParameter("body_size", size);
            _material.SetShaderParameter("region_size", canvas);
        }

        private void OnDied(Creature _) => Retire();
        private void OnCombatEnded(CombatRoom _) => Retire();

        internal void Retire()
        {
            Cleanup();
            if (GodotObject.IsInstanceValid(this) && !IsQueuedForDeletion())
                this.QueueFreeSafely();
        }

        public override void _ExitTree() => Cleanup();

        private void Cleanup()
        {
            if (_retired)
                return;
            _retired = true;
            SetProcess(false);
            ReleaseSpine();
            CreatureTint.Clear(_node, TintSource);
            if (_creature is { } creature)
            {
                if (Roots.TryGetValue(creature, out var root) && ReferenceEquals(root, this))
                    Roots.Remove(creature);
                creature.Died -= OnDied;
            }
            if (GodotObject.IsInstanceValid(_node))
                _node.TreeExiting -= Retire;
            CombatManager.Instance.CombatEnded -= OnCombatEnded;
        }
    }
}
