using BaseLib.Abstracts;
using BaseLib.Utils.NodeFactories;
using SakuraMod.SakuraModCode.Extensions;
using Godot;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.TestSupport;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Relics;

namespace SakuraMod.SakuraModCode.Character;

public class SakuraMod : PlaceholderCharacterModel
{
    public const string CharacterId = "SakuraMod";

    public static readonly Color Color = new("ffffff");
    private static readonly Color SakuraMapDrawingColor = new("F4A7C4");
    private const float CombatVisualScale = 0.28f;
    private const float StandeeEntryOffsetY = 16f;
    private const float StandeeEntryDuration = 0.42f;
    private const float StandeeIdleLift = 3.4f;
    private const float StandeeIdleHalfDuration = 1.85f;
    private const float StandeeIdleScalePulse = 0.006f;
    private const float StandeeIdleTilt = 0.005f;
    private static readonly Vector2 CombatVisualSize = new(264f, 468f);
    private static readonly Vector2 CombatVisualTopLeft = new(-132f, -468f);
    private static readonly Vector2 CombatVisualCenter = new(0f, -234f);

    public override Color NameColor => Color;
    public override Color MapDrawingColor => SakuraMapDrawingColor;
    public override CharacterGender Gender => CharacterGender.Neutral;
    public override int StartingHp => 70;

    public override IEnumerable<CardModel> StartingDeck => [
        ModelDb.Card<Gale>(),
        ModelDb.Card<Gale>(),
        ModelDb.Card<Gale>(),
        ModelDb.Card<Gale>(),
        ModelDb.Card<Siege>(),
        ModelDb.Card<Siege>(),
        ModelDb.Card<Siege>(),
        ModelDb.Card<Siege>(),
        ModelDb.Card<Flight>(),
        ModelDb.Card<DreamWand>()
    ];

    public override IReadOnlyList<RelicModel> StartingRelics =>
    [
        ModelDb.Relic<DreamKey>()
    ];

    public override CardPoolModel CardPool => ModelDb.CardPool<SakuraModCardPool>();
    public override RelicPoolModel RelicPool => ModelDb.RelicPool<SakuraModRelicPool>();
    public override PotionPoolModel PotionPool => ModelDb.PotionPool<SakuraModPotionPool>();

    /*  PlaceholderCharacterModel will utilize placeholder basegame assets for most of your character assets until you
        override all the other methods that define those assets. 
        These are just some of the simplest assets, given some placeholders to differentiate your character with. 
        You don't have to, but you're suggested to rename these images. */
    public override Control CustomIcon
    {
        get
        {
            var icon = NodeFactory<Control>.CreateFromResource(CustomIconTexturePath);
            icon.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            return icon;
        }
    }
    public override string CustomIconTexturePath => "character_icon_char_name.png".CharacterUiPath();
    public override string CustomCharacterSelectIconPath => "char_select_char_name.png".CharacterUiPath();
    public override string CustomCharacterSelectLockedIconPath => "char_select_char_name_locked.png".CharacterUiPath();
    public override string CustomMapMarkerPath => "map_marker_char_name.png".CharacterUiPath();
    public override string CustomVisualPath => "charui/sakura_battle_standee.png".ImagePath();
    public override string CustomMerchantAnimPath =>
        Path.Join(MainFile.ResPath, "scenes", "merchant", "sakura_merchant_character.tscn");

    public override NCreatureVisuals CreateCustomVisuals()
    {
        var visualPath = CustomVisualPath;
        MainFile.Logger.Info($"Creating Sakura combat visuals from {visualPath}; exists={ResourceLoader.Exists(visualPath)}");

        try
        {
            var visuals = NodeFactory<NCreatureVisuals>.CreateFromResource(visualPath);
            ApplyCombatVisualLayout(visuals);
            var body = visuals.GetNode<Node2D>("%Visuals");
            StartCombatStandeeAnimation(body);
            var bounds = visuals.GetNode<Control>("%Bounds");
            MainFile.Logger.Info(
                "Created Sakura combat visuals " +
                $"type={visuals.GetType().FullName} " +
                $"name={visuals.Name} " +
                $"children={visuals.GetChildCount()} " +
                $"hasVisuals={visuals.HasNode("%Visuals")} " +
                $"hasBounds={visuals.HasNode("%Bounds")} " +
                $"hasCenterPos={visuals.HasNode("%CenterPos")} " +
                $"bodyClass={body.GetClass()} " +
                $"bodyPos={body.Position} " +
                $"bodyScale={body.Scale} " +
                $"boundsPos={bounds.Position} " +
                $"boundsSize={bounds.Size}");
            return visuals;
        }
        catch (Exception ex)
        {
            MainFile.Logger.Error($"Failed to create Sakura combat visuals from {visualPath}: {ex}");
            throw;
        }
    }

    private static void ApplyCombatVisualLayout(NCreatureVisuals visuals)
    {
        var body = visuals.GetNode<Node2D>("%Visuals");
        body.Visible = true;
        body.Modulate = Colors.White;
        body.Position = CombatVisualCenter;
        body.Scale = Vector2.One * CombatVisualScale;

        var bounds = visuals.GetNode<Control>("%Bounds");
        bounds.Position = CombatVisualTopLeft;
        bounds.Size = CombatVisualSize;
        bounds.CustomMinimumSize = CombatVisualSize;
        bounds.PivotOffset = CombatVisualSize * 0.5f;

        MoveMarker(visuals, "%CenterPos", CombatVisualCenter);
        MoveMarker(visuals, "%IntentPos", new Vector2(0f, CombatVisualTopLeft.Y - 40f));
        MoveMarker(visuals, "%OrbPos", new Vector2(0f, -190f));
        MoveMarker(visuals, "%TalkPos", new Vector2(0f, -420f));
    }

    private static void StartCombatStandeeAnimation(Node2D body)
    {
        if (TestMode.IsOn)
            return;

        TaskHelper.RunSafely(StartCombatStandeeAnimationWhenReady(body));
    }

    private static async Task StartCombatStandeeAnimationWhenReady(Node2D body)
    {
        if (!GodotObject.IsInstanceValid(body))
            return;

        if (!body.IsInsideTree())
            await body.ToSignal(body, Node.SignalName.TreeEntered);

        if (!GodotObject.IsInstanceValid(body) || !body.IsInsideTree())
            return;

        var restPosition = CombatVisualCenter;
        var restScale = Vector2.One * CombatVisualScale;

        body.Position = restPosition + Vector2.Down * StandeeEntryOffsetY;
        body.Scale = restScale * 0.985f;
        body.Rotation = 0f;
        body.Modulate = new Color(1f, 1f, 1f, 0.86f);

        var entry = body.CreateTween().SetParallel();
        entry.TweenProperty(body, "position", restPosition, StandeeEntryDuration)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);
        entry.TweenProperty(body, "scale", restScale, StandeeEntryDuration)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);
        entry.TweenProperty(body, "modulate", Colors.White, StandeeEntryDuration * 0.8f)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Quad);
        entry.Chain().TweenCallback(Callable.From(() => StartCombatStandeeIdle(body, restPosition, restScale)));
    }

    private static void StartCombatStandeeIdle(Node2D body, Vector2 restPosition, Vector2 restScale)
    {
        if (!GodotObject.IsInstanceValid(body) || !body.IsInsideTree())
            return;

        body.Position = restPosition;
        body.Scale = restScale;
        body.Rotation = 0f;
        body.Modulate = Colors.White;

        var liftedPosition = restPosition + Vector2.Up * StandeeIdleLift;
        var settledPosition = restPosition + Vector2.Down * (StandeeIdleLift * 0.35f);
        var liftedScale = restScale * (1f + StandeeIdleScalePulse);
        var settledScale = restScale * (1f - StandeeIdleScalePulse * 0.35f);

        var idle = body.CreateTween().SetLoops();
        idle.TweenProperty(body, "position", liftedPosition, StandeeIdleHalfDuration)
            .SetEase(Tween.EaseType.InOut)
            .SetTrans(Tween.TransitionType.Sine);
        idle.Parallel().TweenProperty(body, "scale", liftedScale, StandeeIdleHalfDuration)
            .SetEase(Tween.EaseType.InOut)
            .SetTrans(Tween.TransitionType.Sine);
        idle.Parallel().TweenProperty(body, "rotation", -StandeeIdleTilt, StandeeIdleHalfDuration)
            .SetEase(Tween.EaseType.InOut)
            .SetTrans(Tween.TransitionType.Sine);

        idle.TweenProperty(body, "position", settledPosition, StandeeIdleHalfDuration)
            .SetEase(Tween.EaseType.InOut)
            .SetTrans(Tween.TransitionType.Sine);
        idle.Parallel().TweenProperty(body, "scale", settledScale, StandeeIdleHalfDuration)
            .SetEase(Tween.EaseType.InOut)
            .SetTrans(Tween.TransitionType.Sine);
        idle.Parallel().TweenProperty(body, "rotation", StandeeIdleTilt * 0.55f, StandeeIdleHalfDuration)
            .SetEase(Tween.EaseType.InOut)
            .SetTrans(Tween.TransitionType.Sine);
    }

    private static void MoveMarker(Node root, string nodePath, Vector2 position)
    {
        if (root.HasNode(nodePath))
            root.GetNode<Marker2D>(nodePath).Position = position;
    }

    public override string CustomCharacterSelectBg =>
        Path.Join(MainFile.ResPath, "scenes", "screens", "char_select", "char_select_bg_sakura_mod.tscn");

    public override CustomEnergyCounter? CustomEnergyCounter => new(
        layer => layer == 1
            ? "charui/combat_energy_counter_badge.png".ImagePath()
            : "charui/empty_energy_counter_layer.png".ImagePath(),
        new Color("24343d"),
        new Color("d9edf2"));
}
