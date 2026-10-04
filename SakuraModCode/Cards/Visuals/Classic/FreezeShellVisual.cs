using System.Runtime.CompilerServices;
using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.TestSupport;
using SakuraMod.SakuraModCode.Powers;
using SakuraMod.SakuraModCode.FourthAct.Water.Powers;

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>Projects live Frostbite and Freeze onto one creature-local ice visual and creature tint.</summary>
internal static partial class FreezeShellVisual
{
    internal const string ShaderPath = MainFile.ResPath + "/shaders/card_vfx/freeze_shell.gdshader";
    internal const string RootName = "SakuraFreezeShell";
    internal static IReadOnlyList<string> AssetPaths { get; } = [ShaderPath];
    private const float GrowSeconds = 0.30f;
    private const float BreakSeconds = 0.48f;
    private const float HitSeconds = 0.24f;
    private const float FrostFadeSeconds = 0.20f;
    // Frostbite reaches at most this share of the full Freeze tint.
    private const float FrostTintShare = 0.35f;
    // Multiplies the creature's own RGB: lifts blue and pales warm colours.
    private static readonly Vector3 FrozenTint = new(0.80f, 1.02f, 1.30f);
    private static readonly ConditionalWeakTable<Creature, ShellRoot> Roots = [];

    internal static void Mount(Creature creature)
    {
        if (TestMode.IsOn || !SakuraModConfig.IsCardVfxEnabled()
            || !creature.IsAlive
            || (!IsFrozen(creature) && creature.GetPowerAmount<SakuraFrostbitePower>() <= 0))
            return;
        if (Roots.TryGetValue(creature, out var existing))
        {
            if (GodotObject.IsInstanceValid(existing) && !existing.IsQueuedForDeletion())
            {
                existing.RefreshFreeze();
                return;
            }
            Roots.Remove(creature);
        }

        if (NCombatRoom.Instance is not { } room
            || room.GetCreatureNode(creature) is not { } node
            || !GodotObject.IsInstanceValid(node.Visuals)
            || CelVfxGeometry.ResolveCaster(node) is null)
            return;

        ShellRoot? root = null;
        try
        {
            var material = new ShaderMaterial { Shader = PreloadManager.Cache.GetAsset<Shader>(ShaderPath) };
            material.SetShaderParameter("growth", 0f);
            material.SetShaderParameter("release", 0f);
            material.SetShaderParameter("hit", 0f);
            material.SetShaderParameter("frost", 0f);
            material.SetShaderParameter("frost_pulse", 0f);
            material.SetShaderParameter("release_seconds", BreakSeconds);
            root = new ShellRoot
            {
                Name = $"{RootName}_{creature.CombatId}",
                ZAsRelative = true,
                ZIndex = 0
            };
            var ice = new ColorRect
            {
                Name = "Ice",
                Color = Colors.White,
                Material = material,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            root.AddChild(ice);
            // Directly after the body: the creature's own health bar, Block,
            // Powers, intents and reticle draw over the ice, and the body tint
            // on Visuals does not reach it.
            node.AddChildSafely(root);
            node.MoveChildSafely(root, node.Visuals.GetIndex() + 1);
            Roots.Add(creature, root);
            root.Start(creature, node, ice, material);
        }
        catch (Exception exception)
        {
            root?.Retire();
            MainFile.Logger.Error($"Could not mount Freeze shell: {exception}");
        }
    }

    private static bool IsFrozen(Creature creature) =>
        creature.GetPowerAmount<ClassicFreezePower>() > 0 || creature.GetPowerAmount<WaterFrozenPower>() > 0;

    internal static void NotifyFrostbite(Creature creature, bool pulse)
    {
        Mount(creature);
        if (pulse && !TestMode.IsOn && Roots.TryGetValue(creature, out var root)
            && GodotObject.IsInstanceValid(root) && !root.IsQueuedForDeletion())
            root.PulseFrost();
    }

    internal static void NotifyHit(Creature creature)
    {
        if (!TestMode.IsOn && Roots.TryGetValue(creature, out var root)
            && GodotObject.IsInstanceValid(root) && !root.IsQueuedForDeletion())
            root.FlashHit();
    }

    private sealed partial class ShellRoot : Node2D
    {
        private Creature? _creature;
        private NCreature _node = null!;
        private ICombatState? _combat;
        private ColorRect _ice = null!;
        private ShaderMaterial _material = null!;
        private Vector2 _size;
        private float _growth;
        private float _release;
        private float _hit;
        private float _frost;
        private float _frostPulse;
        private bool _shellShown;
        private bool _retired;
        private Color _baseModulate = Colors.White;
        private Color? _writtenModulate;

        internal void Start(Creature creature, NCreature node, ColorRect ice, ShaderMaterial material)
        {
            _creature = creature;
            _node = node;
            _combat = creature.CombatState;
            _ice = ice;
            _material = material;
            creature.Died += OnDied;
            node.TreeExiting += Retire;
            CombatManager.Instance.CombatEnded += OnCombatEnded;
            FollowBody();
            RefreshFreeze();
        }

        internal void RefreshFreeze()
        {
            if (_retired || _creature is null || !IsFrozen(_creature)
                || (_shellShown && _release <= 0f))
                return;
            // A newly applied Freeze can interrupt the old shell's release.
            _shellShown = true;
            _release = 0f;
            _growth = 0f;
            _hit = 0f;
            _material.SetShaderParameter("release", 0f);
            _material.SetShaderParameter("growth", 0f);
            PlayFormationSound();
        }

        internal void PulseFrost() => _frostPulse = 1f;

        internal void FlashHit()
        {
            _hit = 1f;
            _material.SetShaderParameter("hit", _hit);
        }

        private static void PlayFormationSound()
        {
            try { SfxCmd.Play("event:/sfx/characters/defect/defect_frost_channel", 0.55f); }
            catch (Exception exception) { MainFile.Logger.Error($"Could not play Freeze sound: {exception}"); }
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
                if (IsFrozen(_creature))
                {
                    RefreshFreeze();
                    _growth = Math.Min(1f, _growth + dt / GrowSeconds);
                }
                else if (_shellShown)
                {
                    _release = Math.Min(1f, _release + dt / BreakSeconds);
                    if (_release >= 1f)
                    {
                        _shellShown = false;
                        _growth = 0f;
                    }
                }
                var frostbite = _creature.GetPower<SakuraFrostbitePower>();
                var targetFrost = frostbite is { Amount: > 0 }
                    ? Math.Clamp((float)frostbite.Amount / frostbite.CurrentFreezeThreshold, 0f, 1f)
                    : 0f;
                _frost = Mathf.MoveToward(_frost, targetFrost, dt / FrostFadeSeconds);
                _frostPulse = Math.Max(0f, _frostPulse - dt / 0.28f);
                if (!_shellShown && targetFrost <= 0f && _frost <= 0f)
                {
                    Retire();
                    return;
                }
                _hit = Math.Max(0f, _hit - dt / HitSeconds);
                FollowBody();
                var shellTint = _shellShown
                    ? Mathf.SmoothStep(0f, 1f, _growth) * (1f - Mathf.SmoothStep(0f, 0.12f, _release))
                    : 0f;
                ApplyTint(Math.Max(_frost * FrostTintShare, shellTint), shellTint > 0f ? _hit : 0f);
                _material.SetShaderParameter("growth", _growth);
                _material.SetShaderParameter("release", _release);
                _material.SetShaderParameter("hit", _hit);
                _material.SetShaderParameter("frost", _frost);
                _material.SetShaderParameter("frost_pulse", _frostPulse);
            }
            catch (Exception exception)
            {
                Retire();
                MainFile.Logger.Error($"Could not update Freeze shell: {exception}");
            }
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
            // Side margin holds the outward-leaning crystals and thrown pieces;
            // vertical margin holds the ground frost and falling pieces.
            var canvas = size + new Vector2(size.Y * 0.5f + 340f, 220f);
            _ice.Size = canvas;
            _ice.Position = -canvas * 0.5f;
            _material.SetShaderParameter("body_size", size);
            _material.SetShaderParameter("region_size", canvas);
        }

        // Only RGB is multiplied; alpha stays with native fades. Whatever RGB the
        // creature had before (e.g. native background dimming) is the base.
        private void ApplyTint(float amount, float hit)
        {
            if (!GodotObject.IsInstanceValid(_node.Visuals))
                return;
            var visuals = _node.Visuals;
            var current = visuals.Modulate;
            if (_writtenModulate is not { } written || !SameRgb(current, written))
                _baseModulate = current;
            if (amount <= 0f)
            {
                RestoreTint();
                return;
            }
            var factor = Vector3.One.Lerp(FrozenTint, amount);
            factor = factor.Lerp(new Vector3(1.4f, 1.4f, 1.4f), 0.25f * hit);
            var tinted = new Color(_baseModulate.R * factor.X, _baseModulate.G * factor.Y,
                _baseModulate.B * factor.Z, current.A);
            visuals.Modulate = tinted;
            _writtenModulate = tinted;
        }

        private void RestoreTint()
        {
            if (_writtenModulate is not { } written)
                return;
            _writtenModulate = null;
            if (!GodotObject.IsInstanceValid(_node) || !GodotObject.IsInstanceValid(_node.Visuals))
                return;
            var current = _node.Visuals.Modulate;
            // Leave a colour some other writer set after ours.
            if (SameRgb(current, written))
                _node.Visuals.Modulate = new Color(_baseModulate.R, _baseModulate.G, _baseModulate.B, current.A);
        }

        private static bool SameRgb(Color a, Color b) =>
            Math.Abs(a.R - b.R) < 0.0001f && Math.Abs(a.G - b.G) < 0.0001f && Math.Abs(a.B - b.B) < 0.0001f;

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
            RestoreTint();
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
