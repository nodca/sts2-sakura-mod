using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>
/// The element spells' talisman: the played card is the talisman, and its
/// central glyph catches light in the element's colour before the damage lands.
/// </summary>
/// <remarks>
/// The glyph is a separate layer in the UI that reads the card face's global
/// transform every frame; the vanilla card node is never touched. Burning the
/// talisman is left to vanilla's exhaust fire, which plays on the same card
/// right after it resolves.
/// </remarks>
internal sealed class SpellTalismanVfx : CelVfxSession
{
    internal const string ScenePath =
        MainFile.ResPath + "/scenes/combat/card_vfx/spell_talisman_vfx.tscn";
    internal const string ShaderPath =
        MainFile.ResPath + "/shaders/card_vfx/spell_talisman.gdshader";
    internal const string GlyphMaskDirectory =
        MainFile.ResPath + "/images/card_vfx/spell_talisman/";

    /// <summary>Vanilla's power-card play sound, which carries a fire layer.</summary>
    internal const string IgniteSfx = "event:/sfx/ui/cards/card_movement_B_power";

    /// <summary>The full-face renderer's face node on <c>NCard.Body</c>.</summary>
    internal const string FaceNodeName = "ClassicSakuraFace";

    /// <summary>How long the glyph takes to catch, bottom to top.</summary>
    internal const float CatchDuration = 0.20f;

    /// <summary>
    /// When damage may resolve: the glyph fully lit and held for one stepped
    /// frame, so the lit state is seen before the hit takes the eye away.
    /// </summary>
    internal const float ReleaseAt = CatchDuration + 1f / StepFrequency;

    private const float FadeDuration = 0.2f;
    private const int VfxZIndex = 3000;

    private static bool _loadFailureLogged;

    private readonly NCard _card;
    private readonly Node2D _anchor;
    private readonly ColorRect _glyph;
    private readonly ShaderMaterial _material;
    private TextureRect? _face;
    private float _sinceIgnite;
    private bool _faded;

    private SpellTalismanVfx(Node2D root, NCombatRoom room, NCard card, Texture2D mask, Color colour)
        : base(root, room)
    {
        _card = card;
        _anchor = root.GetNode<Node2D>("%Anchor");
        _glyph = root.GetNode<ColorRect>("%Glyph");
        _material = CelVfxGeometry.DuplicateMaterial(_glyph, "spell talisman glyph");
        _material.SetShaderParameter("glyph_mask", mask);
        _material.SetShaderParameter("glyph_colour", colour);
        _material.SetShaderParameter("glow", 0f);
        _material.SetShaderParameter("opacity", 1f);
        Follow();
    }

    protected override IEnumerable<ShaderMaterial> Materials => [_material];

    /// <summary>
    /// Safety net, not a timer: the catch, the release, and the fade take about
    /// half a second.
    /// </summary>
    protected override float MaximumLifetime => 1.5f;

    /// <summary>The scene plus this card's own glyph mask.</summary>
    internal static IReadOnlyList<string> RunAssetPaths(CardModel card) =>
        GlyphFor(card) is { } glyph ? [ScenePath, GlyphMaskPath(glyph.Stem)] : [];

    internal static string GlyphMaskPath(string stem) => GlyphMaskDirectory + stem + "_glyph.png";

    /// <summary>The mask stem and lit colour of each element spell.</summary>
    internal static (string Stem, Color Colour)? GlyphFor(CardModel card) => card switch
    {
        SpellHuoShen => ("huoshen", new Color(1f, 0.42f, 0.16f)),
        SpellLeiDi => ("leidi", new Color(1f, 0.85f, 0.29f)),
        SpellShuiLong => ("shuilong", new Color(0.31f, 0.78f, 1f)),
        SpellFengHua => ("fenghua", new Color(0.37f, 0.89f, 0.60f)),
        _ => null
    };

    internal static Task PlayOrResolveAsync(CardModel card, Func<Cues, Task> resolveGameplay)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(resolveGameplay);

        return CelVfxSession.PlayOrResolveAsync(
            "Spell talisman",
            () => TryCreate(card),
            session => Task.FromResult(session.Ignite()),
            scope => resolveGameplay(new Cues(scope)),
            session => session.FadeAndDispose(),
            session => session.Dispose());
    }

    internal sealed class Cues(CueScope<SpellTalismanVfx> scope)
    {
        /// <summary>
        /// Completes once the glyph has caught and held; damage resolves after
        /// it. Returns at once when nothing is drawn.
        /// </summary>
        internal Task Release() =>
            scope.InvokeAsync("release", session => session.ReleaseAsync());
    }

    private static SpellTalismanVfx? TryCreate(CardModel card)
    {
        if (GlyphFor(card) is not { } glyph
            || !TryPrepare(
                "Spell talisman",
                () => LoadResources(glyph.Stem),
                out var room,
                out _,
                out var resources)
            || room.Ui is not { } ui
            || !TryFindNativePlayedCard(room, card, out var nativeCard))
        {
            return null;
        }

        Node2D? root = null;
        try
        {
            root = resources.Scene.Instantiate<Node2D>();
            root.Name = "SakuraSpellTalismanVfx";
            root.ZAsRelative = false;
            root.ZIndex = VfxZIndex;
            ui.AddChildSafely(root);

            var session = new SpellTalismanVfx(root, room, nativeCard, resources.Mask, glyph.Colour);
            session.StartClock();
            return session;
        }
        catch (Exception exception)
        {
            LogLoadFailure(exception);
            root?.QueueFreeSafely();
            return null;
        }
    }

    /// <summary>Sound, the catch sweep, and the per-frame follow.</summary>
    private bool Ignite()
    {
        if (!IsActive())
            return false;

        SfxCmd.Play(IgniteSfx);
        var tween = Track(Root.CreateTween());
        tween.TweenMethod(
                Callable.From<float>(value => _material.SetShaderParameter("glow", value)),
                0f,
                1f,
                CatchDuration)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);
        tween.TweenCallback(Callable.From(() => BeginHold()));
        TaskHelper.RunSafely(FollowCard());
        return IsActive();
    }

    private async Task ReleaseAsync()
    {
        while (_sinceIgnite < ReleaseAt)
        {
            if (!IsActive())
                return;
            await Root.AwaitProcessFrame();
        }
    }

    /// <summary>
    /// Keeps the glyph on the card while vanilla's play tween is still moving
    /// it, and ends the session the moment the card node goes away.
    /// </summary>
    private async Task FollowCard()
    {
        try
        {
            while (IsActive())
            {
                if (!Follow())
                {
                    Dispose();
                    return;
                }
                _sinceIgnite += await Root.AwaitProcessFrame();
            }
        }
        catch (OperationCanceledException) when (!IsActive())
        {
        }
    }

    private bool Follow()
    {
        if (!GodotObject.IsInstanceValid(_card) || !_card.IsInsideTree() || _card.IsQueuedForDeletion())
            return false;

        if (_face is null || !GodotObject.IsInstanceValid(_face))
            _face = GodotObject.IsInstanceValid(_card.Body)
                ? _card.Body.GetNodeOrNull<TextureRect>(FaceNodeName)
                : null;

        if (_face is { } face && face.IsInsideTree())
        {
            _anchor.GlobalTransform = face.GetGlobalTransform();
            _glyph.Size = face.Size;
        }
        else
        {
            _anchor.GlobalTransform = _card.GetGlobalTransform();
            _glyph.Size = _card.GetCurrentSize();
        }
        return true;
    }

    /// <summary>
    /// The glyph fades while vanilla's exhaust takes the card; this never holds
    /// combat up.
    /// </summary>
    private void FadeAndDispose()
    {
        if (_faded || !IsActive())
        {
            Dispose();
            return;
        }

        _faded = true;
        var fade = Track(Root.CreateTween());
        fade.TweenMethod(
            Callable.From<float>(value => _material.SetShaderParameter("opacity", value)),
            1f,
            0f,
            FadeDuration);
        fade.TweenCallback(Callable.From(Dispose));
    }

    private static (PackedScene Scene, Texture2D Mask) LoadResources(string stem) =>
        (PreloadManager.Cache.GetScene(ScenePath),
            PreloadManager.Cache.GetAsset<Texture2D>(GlyphMaskPath(stem)));

    private static void LogLoadFailure(Exception exception)
    {
        if (_loadFailureLogged)
            return;

        _loadFailureLogged = true;
        MainFile.Logger.Error($"Could not create spell talisman VFX from {ScenePath} and {ShaderPath}: {exception}");
    }
}
