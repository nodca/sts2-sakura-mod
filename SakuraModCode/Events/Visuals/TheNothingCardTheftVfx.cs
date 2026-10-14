using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;
using MegaCrit.Sts2.Core.TestSupport;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Extensions;
using SakuraMod.SakuraModCode.FourthAct.Visuals;
using SakuraMod.SakuraModCode.FourthAct.Visuals.Presentation;

namespace SakuraMod.SakuraModCode.Events.Visuals;

// The Nothing takes cards (Destruction). Each one shows its face for a moment where it was —
// in the hand, or popping out of its pile — turns over to a Clow card back (Clow cards) or a
// Sakura card back (everything else), and drifts, tilted and slowly turning, into her sphere.
// The sphere cuts it off flat where it crosses the boundary (card_sphere_clip), and its
// surface ripples from where the card went in. Cards are staggered; the Voids wait for the last one.
//
// Begin runs before CardPileCmd.RemoveFromCombat(skipVisuals: true): it needs each card's
// pile and takes hand cards off the table the way native removal does. Only the local
// player's cards are shown, like native pile visuals. Fails open: nothing here can keep the
// cards from being removed, and WaitAsync never waits longer than MaxWait.
internal sealed partial class TheNothingCardTheftVfx : Node2D
{
    internal static readonly string SakuraBackPath = "cards/classic/backs/sakura_back.png".ImagePath();
    internal static readonly string ClowBackPath = "cards/classic/backs/clow_back.png".ImagePath();
    internal const string ClipShaderPath = MainFile.ResPath + "/shaders/fourth_act/card_sphere_clip.gdshader";

    internal static IEnumerable<string> AssetPaths => [SakuraBackPath, ClowBackPath, ClipShaderPath];

    private const int VfxZIndex = 3000;
    private const float Stagger = 0.3f;
    private const float Reveal = 0.2f;
    private const float Flip = 0.25f;
    private const float Drift = 1.45f;
    private const float FastSpeed = 1.6f;
    private const float MaxWait = 4f;
    private const float HandLift = 30f;
    private const float PileRise = 70f;
    private const float ArcLift = 220f;
    private const float Tilt = 0.42f;
    private const float Sway = 0.2f;
    private const float EndScale = 0.55f;

    private static readonly StringName CenterParam = "sphere_center";
    private static readonly StringName RadiusParam = "sphere_radius";

    private readonly Creature _nothing;
    private readonly List<Flight> _flights = [];
    private readonly float _speed;
    private float _elapsed;
    private bool _done;

    private TheNothingCardTheftVfx(Creature nothing)
    {
        _nothing = nothing;
        _speed = SaveManager.Instance?.PrefsSave.FastMode == FastModeType.Fast ? FastSpeed : 1f;
        Name = "TheNothingCardTheftVfx";
        ZIndex = VfxZIndex;
        ZAsRelative = false;
    }

    internal static TheNothingCardTheftVfx? Begin(IReadOnlyList<CardModel> cards, Creature nothing)
    {
        if (TestMode.IsOn || NCombatRoom.Instance is not { } room)
            return null;

        try
        {
            var shown = cards.Where(static card => card.Pile is not null && LocalContext.IsMe(card.Owner)).ToList();
            if (shown.Count == 0)
                return null;

            var vfx = new TheNothingCardTheftVfx(nothing);
            room.Ui.AddChildSafely(vfx);
            var shader = PreloadManager.Cache.GetAsset<Shader>(ClipShaderPath);
            for (var i = 0; i < shown.Count; i++)
            {
                if (vfx.CreateFlight(shown[i], i * Stagger, shader) is { } flight)
                    vfx._flights.Add(flight);
            }

            if (vfx._flights.Count == 0)
            {
                vfx.QueueFreeSafely();
                return null;
            }

            CombatManager.Instance.CombatEnded += vfx.OnCombatEnded;
            FourthActEnemyAudio.Play(FourthActAudioCue.NothingSteal);
            return vfx;
        }
        catch (Exception exception)
        {
            MainFile.Logger.Warn($"The Nothing card theft visuals failed open: {exception.Message}");
            return null;
        }
    }

    // Returns once every card has gone into the sphere, or after MaxWait.
    internal async Task WaitAsync()
    {
        try
        {
            // Finish frees this node before the next frame; the awaiter must outlive it.
            var tree = GetTree();
            var waited = 0f;
            while (!_done && IsInstanceValid(this) && IsInsideTree() && waited < MaxWait)
            {
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                waited += (float)tree.Root.GetProcessDeltaTime();
            }
        }
        catch (Exception exception)
        {
            MainFile.Logger.Warn($"The Nothing card theft wait failed open: {exception.Message}");
        }
    }

    public override void _Process(double delta)
    {
        if (_done)
            return;

        try
        {
            _elapsed += (float)delta * _speed;
            var sphere = Sphere();
            var remaining = 0;
            foreach (var flight in _flights)
            {
                if (!flight.Finished)
                    Advance(flight, _elapsed - flight.Delay, sphere);
                if (!flight.Finished)
                    remaining++;
            }

            if (remaining == 0)
                Finish();
        }
        catch (Exception exception)
        {
            MainFile.Logger.Warn($"The Nothing card theft visuals failed open: {exception.Message}");
            Finish();
        }
    }

    public override void _ExitTree()
    {
        _done = true;
        if (CombatManager.Instance is { } combat)
            combat.CombatEnded -= OnCombatEnded;
    }

    private Flight? CreateFlight(CardModel card, float delay, Shader shader)
    {
        var holder = new Node2D { Name = $"Card{_flights.Count}" };
        AddChild(holder);
        var back = new Sprite2D
        {
            Texture = PreloadManager.Cache.GetAsset<Texture2D>(card is SakuraSourceCard { IsClowCard: true } ? ClowBackPath : SakuraBackPath),
            Visible = false,
            Material = new ShaderMaterial { Shader = shader }
        };

        NCard? face;
        var fromPile = false;
        if (NCard.FindOnTable(card) is { } table)
        {
            holder.GlobalPosition = table.GlobalPosition;
            face = TemporaryDissolveVfx.AttachVisibleCard(holder, card, table);
            holder.Rotation = face.Rotation;
            face.Rotation = 0f;
            face.Position = Vector2.Zero;
        }
        else if (card.Pile?.Type is { } pile && NCard.Create(card) is { } created)
        {
            face = created;
            holder.AddChild(face);
            face.UpdateVisuals(pile, CardPreviewMode.Normal);
            holder.Position = pile.GetTargetPosition(face);
            face.Position = Vector2.Zero;
            face.Scale = Vector2.One;
            fromPile = true;
        }
        else
        {
            holder.QueueFreeSafely();
            return null;
        }

        face.MouseFilter = Control.MouseFilterEnum.Ignore;
        var size = face.GetCurrentSize();
        var backScale = size.Y / back.Texture.GetSize().Y;
        back.Scale = new Vector2(backScale, backScale);
        holder.AddChild(back);
        var baseScale = fromPile ? 0.62f : 1f;
        holder.Scale = Vector2.One * (fromPile ? 0.3f : 1f);
        holder.Modulate = fromPile ? new Color(1f, 1f, 1f, 0f) : Colors.White;
        return new Flight(holder, face, back, (ShaderMaterial)back.Material, delay, holder.Position, holder.Rotation,
            baseScale, fromPile, size.Length() * 0.5f);
    }

    private void Advance(Flight flight, float t, (Vector2 Center, float Radius)? sphere)
    {
        if (t < 0f)
            return;

        var holder = flight.Holder;
        if (!IsInstanceValid(holder))
        {
            flight.Finished = true;
            return;
        }

        // Reveal: the face where it was (a hand card lifts, a pile card pops out).
        var reveal = Ease(Mathf.Clamp(t / Reveal, 0f, 1f));
        var raised = flight.Start + Vector2.Up * (flight.FromPile ? PileRise : HandLift) * reveal;
        if (t < Reveal + Flip)
        {
            holder.Position = raised;
            var scale = flight.FromPile ? Mathf.Lerp(0.3f, flight.BaseScale, reveal) : flight.BaseScale;
            holder.Modulate = new Color(1f, 1f, 1f, flight.FromPile ? reveal : 1f);
            // Turn over: squash to an edge, swap the face for its back, open again.
            var turn = Mathf.Clamp((t - Reveal) / Flip, 0f, 1f);
            var width = Mathf.Abs(Mathf.Cos(turn * Mathf.Pi));
            holder.Scale = new Vector2(scale * Mathf.Max(width, 0.02f), scale);
            ShowBack(flight, turn >= 0.5f);
            WriteClip(flight, sphere);
            return;
        }

        ShowBack(flight, true);
        holder.Modulate = Colors.White;
        if (sphere is not { } ball)
        {
            Complete(flight, null);
            return;
        }

        // Drift into the sphere along a raised curve, slow at first and drawn in at the end.
        var u = Mathf.Clamp((t - Reveal - Flip) / Drift, 0f, 1f);
        var e = u * u * (1.6f - 0.6f * u);
        var control = (raised + ball.Center) * 0.5f + Vector2.Up * ArcLift;
        var position = QuadraticBezier(raised, control, ball.Center, e);
        holder.Position = position;
        var heading = (ball.Center - raised).X < 0f ? -1f : 1f;
        holder.Rotation = Mathf.Lerp(flight.StartRotation, heading * Tilt, Ease(Mathf.Min(1f, u * 2.5f)))
                          + Mathf.Sin((t - Reveal - Flip) * 2.2f) * Sway * (1f - u);
        var drifting = flight.BaseScale * Mathf.Lerp(1f, EndScale, e);
        holder.Scale = new Vector2(drifting, drifting);
        WriteClip(flight, sphere);

        var distance = position.DistanceTo(ball.Center);
        if (!flight.Entered && distance - flight.HalfDiagonal * drifting < ball.Radius)
        {
            flight.Entered = true;
            flight.EntryPoint = ball.Center + (position - ball.Center).Normalized() * ball.Radius;
            FourthActEnemyAudio.Play(FourthActAudioCue.NothingCardErase);
        }

        if (distance + flight.HalfDiagonal * drifting < ball.Radius || u >= 1f)
            Complete(flight, flight.EntryPoint ?? ball.Center + (raised - ball.Center).Normalized() * ball.Radius);
    }

    private static void ShowBack(Flight flight, bool back)
    {
        if (flight.Back.Visible == back)
            return;
        flight.Back.Visible = back;
        if (IsInstanceValid(flight.Face))
            flight.Face.Visible = !back;
    }

    private void WriteClip(Flight flight, (Vector2 Center, float Radius)? sphere)
    {
        if (sphere is not { } ball)
            return;
        // The clip shader works in viewport coordinates.
        var toViewport = GetGlobalTransformWithCanvas();
        flight.Clip.SetShaderParameter(CenterParam, toViewport * ball.Center);
        flight.Clip.SetShaderParameter(RadiusParam, ball.Radius * toViewport.X.Length());
    }

    private void Complete(Flight flight, Vector2? entry)
    {
        flight.Finished = true;
        if (entry is { } point && Sphere() is { } ball && _nothing.Monster is ClassicTheNothingMonster nothing)
            nothing.RippleWhereCardEntered((point - ball.Center) / ball.Radius * 0.9f);

        if (IsInstanceValid(flight.Face))
            flight.Face.QueueFreeSafely();
        flight.Holder.QueueFreeSafely();
    }

    // Her sphere in this node's space: centre and radius.
    private (Vector2 Center, float Radius)? Sphere()
    {
        if (NCombatRoom.Instance?.GetCreatureNode(_nothing) is not { } node || !node.IsInsideTree())
            return null;

        var toViewport = node.GetCanvasTransform();
        var center = toViewport * node.VfxSpawnPosition;
        var radius = ClassicTheNothingMonster.SphereRadius * node.Visuals.GlobalScale.X * toViewport.X.Length();
        var fromViewport = GetGlobalTransformWithCanvas().AffineInverse();
        return (fromViewport * center, radius * fromViewport.X.Length());
    }

    private void Finish()
    {
        if (_done)
            return;
        _done = true;
        foreach (var flight in _flights.Where(static flight => !flight.Finished))
            Complete(flight, null);
        this.QueueFreeSafely();
    }

    private void OnCombatEnded(CombatRoom _) => Finish();

    private static float Ease(float x) => 1f - (1f - x) * (1f - x);

    private static Vector2 QuadraticBezier(Vector2 a, Vector2 b, Vector2 c, float t)
    {
        var ab = a.Lerp(b, t);
        var bc = b.Lerp(c, t);
        return ab.Lerp(bc, t);
    }

    private sealed class Flight(
        Node2D holder,
        NCard face,
        Sprite2D back,
        ShaderMaterial clip,
        float delay,
        Vector2 start,
        float startRotation,
        float baseScale,
        bool fromPile,
        float halfDiagonal)
    {
        internal Node2D Holder { get; } = holder;
        internal NCard Face { get; } = face;
        internal Sprite2D Back { get; } = back;
        internal ShaderMaterial Clip { get; } = clip;
        internal float Delay { get; } = delay;
        internal Vector2 Start { get; } = start;
        internal float StartRotation { get; } = startRotation;
        internal float BaseScale { get; } = baseScale;
        internal bool FromPile { get; } = fromPile;
        internal float HalfDiagonal { get; } = halfDiagonal;
        internal bool Entered { get; set; }
        internal Vector2? EntryPoint { get; set; }
        internal bool Finished { get; set; }
    }
}
