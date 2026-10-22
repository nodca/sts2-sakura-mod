using System.Runtime.CompilerServices;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>
/// The single writer of a creature's <c>Visuals.Modulate</c> RGB. Each presentation
/// source submits its own RGB factor; the writer multiplies them onto the colour the
/// creature had before any of them, so two sources never re-base on each other.
/// </summary>
internal static class CreatureTint
{
    private static readonly ConditionalWeakTable<NCreature, Composition> States = [];

    internal static void Set(NCreature node, string source, Vector3 factor)
    {
        if (!GodotObject.IsInstanceValid(node) || !GodotObject.IsInstanceValid(node.Visuals))
            return;
        var state = States.GetOrCreateValue(node);
        node.Visuals.Modulate = state.Set(source, factor, node.Visuals.Modulate);
    }

    internal static void Clear(NCreature? node, string source)
    {
        // A visual that failed before mounting has no node yet.
        if (node is null || !States.TryGetValue(node, out var state))
            return;
        if (!GodotObject.IsInstanceValid(node) || !GodotObject.IsInstanceValid(node.Visuals))
        {
            States.Remove(node);
            return;
        }
        if (state.Clear(source, node.Visuals.Modulate) is { } colour)
            node.Visuals.Modulate = colour;
        if (state.IsEmpty)
            States.Remove(node);
    }

    /// <summary>Node-free composition rules, shared by every source of one creature.</summary>
    internal sealed class Composition
    {
        private readonly Dictionary<string, Vector3> _factors = new(StringComparer.Ordinal);
        private Color _base = Colors.White;
        private Color? _written;

        internal bool IsEmpty => _factors.Count == 0;

        /// <summary>Records the factor and returns the colour to write.</summary>
        internal Color Set(string source, Vector3 factor, Color current)
        {
            Rebase(current);
            _factors[source] = factor;
            return Write(current);
        }

        /// <summary>
        /// Drops the factor. Returns the colour to write, or null when the current
        /// colour belongs to some other writer and must be left alone.
        /// </summary>
        internal Color? Clear(string source, Color current)
        {
            if (!_factors.Remove(source) || _written is not { } written)
                return null;
            if (!SameRgb(current, written))
            {
                // Leave a colour some other writer set after ours.
                if (IsEmpty)
                    _written = null;
                else
                    _base = current;
                return IsEmpty ? null : Write(current);
            }
            if (!IsEmpty)
                return Write(current);
            _written = null;
            return new Color(_base.R, _base.G, _base.B, current.A);
        }

        // Only RGB is multiplied; alpha stays with native fades. Whatever RGB the
        // creature had before (e.g. native background dimming) is the base, and
        // a later change by another writer becomes the new base.
        private void Rebase(Color current)
        {
            if (_written is not { } written || !SameRgb(current, written))
                _base = current;
        }

        private Color Write(Color current)
        {
            var product = Vector3.One;
            foreach (var factor in _factors.Values)
                product *= factor;
            var tinted = new Color(_base.R * product.X, _base.G * product.Y, _base.B * product.Z, current.A);
            _written = tinted;
            return tinted;
        }

        private static bool SameRgb(Color a, Color b) =>
            Math.Abs(a.R - b.R) < 0.0001f && Math.Abs(a.G - b.G) < 0.0001f && Math.Abs(a.B - b.B) < 0.0001f;
    }
}
