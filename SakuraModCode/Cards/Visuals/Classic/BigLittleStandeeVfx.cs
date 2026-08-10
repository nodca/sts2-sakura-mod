using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.FourthAct.Visuals;

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>
/// Shared cel prelude followed by Big or Little's persistent standee transform.
/// The visible size state remains on the combat standee controller; this session
/// owns only its temporary card and timing resources, while the room presenter
/// owns the renewable magic circle.
/// </summary>
internal sealed class BigLittleStandeeVfx : CelVfxSession
{
    private const float LifetimeCap = 3f;
    private static readonly ShaderMaterial[] NoMaterials = [];

    private BigLittleStandeeVfx(Node2D root, NCombatRoom room)
        : base(root, room)
    {
    }

    protected override IEnumerable<ShaderMaterial> Materials => NoMaterials;
    protected override float MaximumLifetime => LifetimeCap;

    internal static async Task PlayOrResolveAsync(
        CardModel card,
        Creature caster,
        SakuraStandeeSizeEffect effect,
        Func<Task> resolveEffect)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(caster);
        ArgumentNullException.ThrowIfNull(resolveEffect);

        var resolved = false;
        async Task ResolveOnce()
        {
            if (resolved)
                return;

            resolved = true;
            await resolveEffect();
        }

        var session = TryCreate();
        if (session is null)
        {
            await ResolveOnce();
            return;
        }

        try
        {
            if (!await session.PlayCelPrelude(card, caster))
                return;

            if (session.Room.GetCreatureNode(caster) is not { } casterNode
                || casterNode.Entity.Player is not { } player
                || !SakuraStarterCompatibility.IsKinomotoSakura(player)
                || SakuraStandeeActionController.TryGet(casterNode) is not { } controller)
            {
                await ResolveOnce();
                return;
            }

            await controller.PlaySizeEffectAsync(effect, ResolveOnce);
        }
        finally
        {
            try
            {
                await ResolveOnce();
            }
            finally
            {
                session.Dispose();
            }
        }
    }

    private static BigLittleStandeeVfx? TryCreate()
    {
        if (!TryPrepare(
                "Big/Little standee",
                static () => true,
                out var room,
                out var container,
                out _))
        {
            return null;
        }

        Node2D? root = null;
        try
        {
            root = new Node2D { Name = "SakuraBigLittleStandeeVfx" };
            container.AddChildSafely(root);
            var session = new BigLittleStandeeVfx(root, room);
            session.StartClock();
            return session;
        }
        catch (Exception exception)
        {
            MainFile.Logger.Error($"Could not create Big/Little standee VFX: {exception}");
            root?.QueueFreeSafely();
            return null;
        }
    }
}
