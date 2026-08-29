using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>
/// Gravitation's on-play session: Clear wand prelude, then cues that open the
/// persistent well and overlay pile-pull tendrils. The Hold visual outlives this
/// session.
/// </summary>
internal sealed class GravitationVfx : CelVfxSession
{
    private const int VfxZIndex = 1;

    private readonly Creature _caster;

    private GravitationVfx(Node2D root, NCombatRoom room, Creature caster)
        : base(root, room)
    {
        _caster = caster;
    }

    protected override IEnumerable<ShaderMaterial> Materials => [];

    protected override float MaximumLifetime => 90f;

    internal static Task PlayOrResolveAsync(
        CardModel card,
        Creature caster,
        Func<Cues, Task> resolveGameplay)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(caster);
        ArgumentNullException.ThrowIfNull(resolveGameplay);

        return CelVfxSession.PlayOrResolveAsync(
            "Gravitation",
            () => TryCreate(caster),
            session => session.PlayCelPrelude(card, caster),
            scope => resolveGameplay(new Cues(scope)),
            session => session.Dispose(),
            session => session.Dispose());
    }

    internal sealed class Cues(CueScope<GravitationVfx> scope)
    {
        internal void OpenWell() =>
            scope.Invoke("open well", static session => session.OpenWell());

        internal void PullFromPile(PileType pileType, CardModel card)
        {
            ArgumentNullException.ThrowIfNull(card);
            scope.Invoke("pull from pile", session => session.PullFromPile(pileType, card));
        }
    }

    private static GravitationVfx? TryCreate(Creature caster)
    {
        if (!TryPrepare("Gravitation", static () => true, out var room, out _, out _))
            return null;

        Node2D? root = null;
        try
        {
            root = new Node2D
            {
                Name = "SakuraGravitationVfx",
                ZAsRelative = false,
                ZIndex = VfxZIndex,
                Modulate = Colors.White
            };
            room.CombatVfxContainer.AddChildSafely(root);
            var session = new GravitationVfx(root, room, caster);
            session.StartClock();
            return session;
        }
        catch (Exception exception)
        {
            MainFile.Logger.Error($"Could not create Gravitation VFX: {exception}");
            root?.QueueFreeSafely();
            return null;
        }
    }

    private void OpenWell() => GravitationHoldVisual.Open(_caster);

    private void PullFromPile(PileType pileType, CardModel card) =>
        GravitationHoldVisual.PullFromPile(_caster, pileType, card);
}
