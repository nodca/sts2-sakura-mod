using System.Runtime.CompilerServices;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.TestSupport;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.FourthAct.Fire.Models;
using SakuraMod.SakuraModCode.FourthAct.Fire.Powers;

namespace SakuraMod.SakuraModCode.FourthAct.Fire.Visuals;

internal sealed partial class LibraVisualController : Node2D
{
    internal const string NodeName = "SakuraLibraVisualController";

    private const int BindFrameLimit = 180;
    private const float CentralX = 960f;
    private const float CentralY = 220f;
    private const float BeamHalfSpan = 737f;
    private const float BeamYOffset = 40f;
    private const float VoteDuration = 0.24f;
    private const float ImbalanceDuration = 0.14f;
    private const float RecenterDuration = 0.30f;
    private static readonly Color Gold = new("e8b94f");
    private static readonly Color HighlightGold = new("fff2b0");
    private static readonly Color ShadowGold = new("9c6f24");
    private static readonly Color RubyRed = new("d82845");
    private static readonly Color RubyDark = new("580f18");
    private static readonly Color RubyGlint = new("ffb0be");
    private static readonly Color Warning = new("ff765e");
    private static readonly Color LineOutline = new("533724");
    private static readonly ConditionalWeakTable<Creature, LibraVisualController> Sessions = new();

    private Sprite2D? _central;
    private Line2D? _leftOutline;
    private Line2D? _leftCore;
    private Line2D? _rightOutline;
    private Line2D? _rightCore;
    private NCreature? _left;
    private NCreature? _right;
    private LibraPendulumPower? _leftPower;
    private LibraPendulumPower? _rightPower;
    private Tween? _motionTween;
    private Tween? _pulseTween;
    private Task _pending = Task.CompletedTask;
    private int _bindFrames;
    private bool _bound;
    private bool _disposed;
    private bool _cueActive;
    private bool _critical;
    private bool _extreme;
    private string? _criticalSide;
    private int _presentationGeneration;
    private bool _assemblyFading;

    public LibraVisualController()
    {
        Name = NodeName;
        ZIndex = 0;
        ZAsRelative = true;
    }

    internal static LibraVisualController? TryGet(Creature creature) =>
        Sessions.TryGetValue(creature, out var controller) && !controller._disposed
            ? controller
            : null;

    internal static async Task WaitForPendingAsync(ICombatState combatState)
    {
        if (TestMode.IsOn || Find(combatState) is not { } controller)
            return;

        try
        {
            await controller._pending.WaitAsync(TimeSpan.FromSeconds(0.8));
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"Libra presentation wait failed open: {ex.Message}");
            controller.AbortPendingPresentation();
        }
    }

    internal static async Task PlayExtremeConfirmationAsync(ICombatState combatState)
    {
        if (TestMode.IsOn || Find(combatState) is not { } controller)
            return;

        try
        {
            await controller.PlayExtremeAsync().WaitAsync(TimeSpan.FromSeconds(0.4));
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"Libra extreme confirmation failed open: {ex.Message}");
            controller.AbortPendingPresentation();
        }
    }

    private static LibraVisualController? Find(ICombatState combatState) =>
        combatState.Enemies
            .Where(static enemy => enemy.Monster is LibraPanMonster)
            .Select(TryGet)
            .FirstOrDefault(static controller => controller is not null);

    public override void _Ready()
    {
        TreeExiting += OnTreeExiting;
        if (TestMode.IsOn)
        {
            SetProcess(false);
            return;
        }

        try
        {
            _central = new Sprite2D
            {
                Name = "CentralAssembly",
                Texture = ResourceLoader.Load<Texture2D>(LibraEnemyAssets.Central)
                    ?? throw new InvalidOperationException("Libra central texture could not be loaded."),
                Position = new Vector2(CentralX, CentralY),
                Scale = Vector2.One * LibraEnemyAssets.CentralScale,
                ZIndex = 1
            };
            _central.AddChild(CreateCrossbeam());
            (_leftOutline, _leftCore) = CreateSuspensionLine("LeftSuspension");
            (_rightOutline, _rightCore) = CreateSuspensionLine("RightSuspension");
            AddChild(_leftOutline);
            AddChild(_leftCore);
            AddChild(_rightOutline);
            AddChild(_rightCore);
            AddChild(_central);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Error($"Failed to create Libra encounter visuals: {ex}");
            Visible = false;
            SetProcess(false);
        }
    }

    public override void _Process(double delta)
    {
        if (_disposed || !Visible)
            return;
        if (!_bound)
        {
            TryBind();
            return;
        }

        var time = Time.GetTicksMsec() * 0.001f;
        if (!_cueActive)
        {
            if (_central is not null)
                _central.Position = new Vector2(CentralX, CentralY + Mathf.Sin(time * 1.7f) * 4f);
            ApplyIdle(_left, Mathf.Sin(time * 1.45f) * Mathf.DegToRad(1.5f));
            ApplyIdle(_right, -Mathf.Sin(time * 1.45f) * Mathf.DegToRad(1.5f));
        }
        RefreshLines(time);
    }

    private void TryBind()
    {
        var room = NCombatRoom.Instance;
        var pans = room?.CreatureNodes
            .Where(static node => node.Entity.Monster is LibraPanMonster)
            .ToArray();
        _left = pans?.FirstOrDefault(static node => node.Entity.SlotName == "LEFT");
        _right = pans?.FirstOrDefault(static node => node.Entity.SlotName == "RIGHT");
        _leftPower = _left?.Entity.GetPower<LibraPendulumPower>();
        _rightPower = _right?.Entity.GetPower<LibraPendulumPower>();
        if (_left is null || _right is null || _leftPower is null || _rightPower is null)
        {
            if (++_bindFrames < BindFrameLimit)
                return;
            MainFile.Logger.Warn("Libra visual controller could not bind both Pan nodes; using native Pan visuals only.");
            Visible = false;
            SetProcess(false);
            return;
        }

        _bound = true;
        Register(_left.Entity);
        Register(_right.Entity);
        _leftPower.PresentationChanged += OnPresentationChanged;
        _rightPower.PresentationChanged += OnPresentationChanged;
        _left.Entity.Died += OnPanDied;
        _right.Entity.Died += OnPanDied;
        CombatManager.Instance.CombatEnded += OnCombatEnded;
        SnapToCurrentState();
        UpdatePlayerFacing("RIGHT");
    }

    private static void UpdatePlayerFacing(string? side)
    {
        if (string.IsNullOrEmpty(side)) return;
        var faceLeft = side == "LEFT";
        if (NCombatRoom.Instance is { } room)
        {
            foreach (var playerNode in room.CreatureNodes.Where(static c => c.Entity.IsPlayer))
                SakuraStandeeVisuals.SetFacing(playerNode, faceLeft);
        }
    }

    private void Register(Creature creature)
    {
        Sessions.Remove(creature);
        Sessions.Add(creature, this);
    }

    private void OnPresentationChanged(LibraPresentationEvent presentation)
    {
        _pending = PlayQueuedAsync(_pending, presentation, _presentationGeneration);
        TaskHelper.RunSafely(_pending);
    }

    private async Task PlayQueuedAsync(
        Task previous,
        LibraPresentationEvent presentation,
        int generation)
    {
        try
        {
            await previous;
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"Libra presentation recovered from an interrupted cue: {ex.Message}");
        }

        if (_disposed || !IsInsideTree() || generation != _presentationGeneration)
            return;

        try
        {
            switch (presentation.Cause)
            {
                case LibraPresentationCause.FacingRecorded:
                    UpdatePlayerFacing(presentation.Side);
                    await PulseSideAsync(presentation.Side, 0.12f, 1.12f, generation);
                    break;
                case LibraPresentationCause.FacingVote:
                    if (presentation.Left == presentation.OldLeft && presentation.Right == presentation.OldRight)
                        await PulseCentralAsync(0.16f, 1.035f, generation);
                    else
                        await MovePansAsync(presentation.Left, presentation.Right, VoteDuration, generation);
                    break;
                case LibraPresentationCause.Imbalance:
                    await MovePansAsync(presentation.Left, presentation.Right, ImbalanceDuration, generation);
                    await PulseSideAsync(presentation.Side, 0.10f, 1.10f, generation);
                    break;
                case LibraPresentationCause.Recenter:
                    if (presentation.Left == presentation.OldLeft && presentation.Right == presentation.OldRight)
                        await PulseCentralAsync(0.20f, 1.04f, generation);
                    else
                    {
                        await MovePansAsync(presentation.Left, presentation.Right, RecenterDuration, generation);
                        await PulseCentralAsync(0.12f, 1.045f, generation);
                    }
                    break;
                case LibraPresentationCause.TierResolved:
                    await PulseSideAsync(
                        presentation.Right > presentation.Left ? "RIGHT" : presentation.Left > presentation.Right ? "LEFT" : null,
                        presentation.Strong ? 0.18f : 0.12f,
                        presentation.Strong ? 1.10f : 1.045f,
                        generation);
                    break;
                case LibraPresentationCause.PanLost:
                    RefreshDeathVisibility();
                    await PulseSideAsync(
                        presentation.Side == "LEFT" ? "RIGHT" : "LEFT",
                        0.18f,
                        1.10f,
                        generation);
                    break;
            }
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"Libra presentation cue failed open: {ex.Message}");
            if (generation == _presentationGeneration)
                SnapToCurrentState();
        }
        finally
        {
            if (generation == _presentationGeneration)
            {
                _cueActive = false;
                RefreshCritical(presentation.Left, presentation.Right);
            }
        }
    }

    internal async Task PlayAttackAsync(Creature actor, Func<Task> resolveAtContact)
    {
        ArgumentNullException.ThrowIfNull(resolveAtContact);
        var node = actor.SlotName == "LEFT" ? _left : _right;
        var other = actor.SlotName == "LEFT" ? _right : _left;
        if (_disposed || node is null || node.Entity.IsDead)
        {
            await resolveAtContact();
            return;
        }

        _cueActive = true;
        var body = node.Body;
        var rest = body.Position;
        var restScale = body.Scale;
        if (other is not null)
            other.Body.Rotation = 0f;
        try
        {
            try
            {
                KillTween(ref _motionTween);
                _motionTween = CreateTween().SetParallel();
                var towardCenter = actor.SlotName == "LEFT" ? 12f : -12f;
                _motionTween.TweenProperty(body, "position", rest + new Vector2(towardCenter, 22f), 0.11f)
                    .SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
                _motionTween.TweenProperty(body, "scale", restScale * new Vector2(1.04f, 0.93f), 0.11f)
                    .SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
                if (_central is not null)
                    _motionTween.TweenProperty(_central, "rotation", -Mathf.Sign(node.Position.X - CentralX) * Mathf.DegToRad(1.5f), 0.11f);
                SetLineModulate(actor.SlotName, new Color(1.35f, 1.2f, 0.75f, 1f));
                await AwaitTweenAsync(_motionTween);
            }
            catch (Exception ex)
            {
                MainFile.Logger.Warn($"Libra attack anticipation failed open: {ex.Message}");
            }

            await resolveAtContact();

            try
            {
                _motionTween = CreateTween().SetParallel();
                _motionTween.TweenProperty(body, "position", rest, 0.16f)
                    .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
                _motionTween.TweenProperty(body, "scale", restScale, 0.16f)
                    .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
                if (_central is not null)
                    _motionTween.TweenProperty(_central, "rotation", CurrentCentralRotation(), 0.16f);
                await AwaitTweenAsync(_motionTween);
                if (other is not null)
                    other.Body.Rotation = 0f;
            }
            catch (Exception ex)
            {
                MainFile.Logger.Warn($"Libra attack recovery failed open: {ex.Message}");
            }
        }
        finally
        {
            body.Position = rest;
            body.Scale = restScale;
            body.Rotation = 0f;
            SetLineModulate(actor.SlotName, Colors.White);
            _cueActive = false;
            SnapToCurrentState();
        }
    }

    private async Task MovePansAsync(int left, int right, float duration, int generation)
    {
        _cueActive = true;
        KillTween(ref _motionTween);
        _motionTween = CreateTween().SetParallel();
        if (_left is { Entity.IsAlive: true })
            _motionTween.TweenProperty(
                _left,
                "global_position:y",
                GlobalY(LibraVisualLayout.CreatureY(left, LibraEnemyAssets.MoonHeight * LibraEnemyAssets.MoonScale)),
                duration);
        if (_right is { Entity.IsAlive: true })
            _motionTween.TweenProperty(
                _right,
                "global_position:y",
                GlobalY(LibraVisualLayout.CreatureY(right, LibraEnemyAssets.SunHeight * LibraEnemyAssets.SunScale)),
                duration);
        if (_central is not null)
            _motionTween.TweenProperty(_central, "rotation", CentralRotation(left, right), duration);
        await AwaitTweenAsync(_motionTween);
        if (generation != _presentationGeneration)
            return;
        _cueActive = false;
        SnapPose(left, right);
    }

    private async Task PulseSideAsync(string? side, float duration, float scale, int generation = -1)
    {
        if (side is null)
        {
            await PulseCentralAsync(duration, scale, generation);
            return;
        }

        var node = side == "LEFT" ? _left : _right;
        if (node is null || node.Entity.IsDead)
            return;
        var body = node.Body;
        var restScale = body.Scale;
        SetLineModulate(side, new Color(1.3f, 1.18f, 0.72f, 1f));
        KillTween(ref _pulseTween);
        _pulseTween = CreateTween();
        _pulseTween.TweenProperty(body, "scale", restScale * scale, duration * 0.45f)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad);
        _pulseTween.TweenProperty(body, "scale", restScale, duration * 0.55f)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
        await AwaitTweenAsync(_pulseTween);
        if (generation >= 0 && generation != _presentationGeneration)
            return;
        body.Scale = restScale;
        SetLineModulate(side, Colors.White);
    }

    private async Task PulseCentralAsync(float duration, float scale, int generation = -1)
    {
        if (_central is null)
            return;
        var restScale = Vector2.One * LibraEnemyAssets.CentralScale;
        KillTween(ref _pulseTween);
        _pulseTween = CreateTween();
        _pulseTween.TweenProperty(_central, "scale", restScale * scale, duration * 0.45f)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad);
        _pulseTween.TweenProperty(_central, "scale", restScale, duration * 0.55f)
            .SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
        await AwaitTweenAsync(_pulseTween);
        if (generation >= 0 && generation != _presentationGeneration)
            return;
        _central.Scale = restScale;
    }

    private async Task PlayExtremeAsync()
    {
        _extreme = true;
        await PulseCentralAsync(0.20f, 1.10f);
        SetLineModulate(_criticalSide, new Color(1.45f, 1.35f, 1.2f, 1f));
    }

    private async Task AwaitTweenAsync(Tween tween)
    {
        if (tween.IsValid())
            await ToSignal(tween, Tween.SignalName.Finished);
    }

    private void SnapToCurrentState()
    {
        var power = _rightPower ?? _leftPower;
        if (power is null)
            return;
        SnapPose(power.Left, power.Right);
        RefreshDeathVisibility();
        RefreshCritical(power.Left, power.Right);
    }

    private void SnapPose(int left, int right)
    {
        if (_left is { Entity.IsAlive: true })
            _left.GlobalPosition = _left.GlobalPosition with
            {
                Y = GlobalY(LibraVisualLayout.CreatureY(left, LibraEnemyAssets.MoonHeight * LibraEnemyAssets.MoonScale))
            };
        if (_right is { Entity.IsAlive: true })
            _right.GlobalPosition = _right.GlobalPosition with
            {
                Y = GlobalY(LibraVisualLayout.CreatureY(right, LibraEnemyAssets.SunHeight * LibraEnemyAssets.SunScale))
            };
        if (_central is not null)
        {
            _central.Position = new Vector2(CentralX, CentralY);
            _central.Rotation = CentralRotation(left, right);
        }
        RefreshLines(Time.GetTicksMsec() * 0.001f);
    }

    private float CurrentCentralRotation()
    {
        var power = _rightPower ?? _leftPower;
        return power is null ? 0f : CentralRotation(power.Left, power.Right);
    }

    private static float CentralRotation(int left, int right) =>
        Mathf.DegToRad(Math.Clamp((right - left) * 0.15f, -3f, 3f));

    private float GlobalY(float encounterY) => ToGlobal(new Vector2(0f, encounterY)).Y;

    private void RefreshCritical(int left, int right)
    {
        _extreme = left is 0 or 10 || right is 0 or 10;
        _critical = !_extreme && (left is 1 or 9 || right is 1 or 9);
        _criticalSide = right > left ? "RIGHT" : left > right ? "LEFT" : null;
    }

    private void RefreshDeathVisibility()
    {
        SetLineVisible("LEFT", _left?.Entity.IsAlive == true);
        SetLineVisible("RIGHT", _right?.Entity.IsAlive == true);
    }

    private void RefreshLines(float time)
    {
        if (_central is null)
            return;
        RefreshLine(_left, _leftOutline, _leftCore, left: true);
        RefreshLine(_right, _rightOutline, _rightCore, left: false);

        var tension = _extreme ? 1f : _critical ? 0.55f + Mathf.Sin(time * 8f) * 0.25f : 0f;
        var color = Gold.Lerp(Warning, tension);
        var onlyLeftAlive = _left?.Entity.IsAlive == true && _right?.Entity.IsAlive != true;
        var onlyRightAlive = _right?.Entity.IsAlive == true && _left?.Entity.IsAlive != true;
        if (_leftCore is not null)
            _leftCore.Width = onlyLeftAlive ? 5f : 3.5f;
        if (_rightCore is not null)
            _rightCore.Width = onlyRightAlive ? 5f : 3.5f;
        if (_criticalSide == "LEFT" && _leftCore is not null)
            _leftCore.DefaultColor = color;
        else if (onlyLeftAlive && _leftCore is not null)
            _leftCore.DefaultColor = Gold.Lightened(0.22f);
        else if (_leftCore is not null)
            _leftCore.DefaultColor = Gold;
        if (_criticalSide == "RIGHT" && _rightCore is not null)
            _rightCore.DefaultColor = color;
        else if (onlyRightAlive && _rightCore is not null)
            _rightCore.DefaultColor = Gold.Lightened(0.22f);
        else if (_rightCore is not null)
            _rightCore.DefaultColor = Gold;
    }

    private void RefreshLine(NCreature? pan, Line2D? outline, Line2D? core, bool left)
    {
        if (pan is null || outline is null || core is null || !pan.Entity.IsAlive)
            return;
        var beamTipGlobal = _central!.ToGlobal(new Vector2(left ? -BeamHalfSpan : BeamHalfSpan, BeamYOffset));
        var halfHeight = (left ? LibraEnemyAssets.MoonHeight : LibraEnemyAssets.SunHeight) * 0.5f;
        var panAnchorGlobal = pan.Body.ToGlobal(new Vector2(left ? -5f : 91f, -halfHeight));
        var points = new[] { ToLocal(beamTipGlobal), ToLocal(panAnchorGlobal) };
        outline.Points = points;
        core.Points = points;
    }

    private static (Line2D Outline, Line2D Core) CreateSuspensionLine(string name)
    {
        var outline = new Line2D
        {
            Name = $"{name}Outline",
            Width = 6f,
            DefaultColor = LineOutline,
            Antialiased = true,
            BeginCapMode = Line2D.LineCapMode.Round,
            EndCapMode = Line2D.LineCapMode.Round,
            ZIndex = 0
        };
        var core = new Line2D
        {
            Name = $"{name}Core",
            Width = 3f,
            DefaultColor = Gold,
            Antialiased = true,
            BeginCapMode = Line2D.LineCapMode.Round,
            EndCapMode = Line2D.LineCapMode.Round,
            ZIndex = 0
        };
        return (outline, core);
    }

    private static Node2D CreateCrossbeam() =>
        new CrossbeamVisual
        {
            Name = "Crossbeam",
            ShowBehindParent = true,
            ZIndex = 0
        };

    private sealed partial class CrossbeamVisual : Node2D
    {
        public override void _Draw()
        {
            // 1. Lower filigree arches and vertical struts
            foreach (var side in new[] { -1f, 1f })
            {
                DrawArmDecorations(side);
            }

            // 2. Main horizontal crossbeam
            var leftEnd = new Vector2(-BeamHalfSpan, BeamYOffset);
            var rightEnd = new Vector2(BeamHalfSpan, BeamYOffset);
            DrawLine(leftEnd, rightEnd, LineOutline, 13f, antialiased: true);
            DrawLine(new Vector2(-BeamHalfSpan + 3f, BeamYOffset + 2.5f), new Vector2(BeamHalfSpan - 3f, BeamYOffset + 2.5f), ShadowGold, 2.5f, antialiased: true);
            DrawLine(new Vector2(-BeamHalfSpan + 2f, BeamYOffset), new Vector2(BeamHalfSpan - 2f, BeamYOffset), Gold, 8f, antialiased: true);
            DrawLine(new Vector2(-BeamHalfSpan + 4f, BeamYOffset - 2.5f), new Vector2(BeamHalfSpan - 4f, BeamYOffset - 2.5f), HighlightGold, 2.2f, antialiased: true);

            // 3. Structural cuffs and ruby medallions on both arms
            foreach (var side in new[] { -1f, 1f })
            {
                // Segment collars / cuffs
                DrawCuff(new Vector2(side * 290f, BeamYOffset));
                DrawCuff(new Vector2(side * 590f, BeamYOffset));

                // Midpoint Clow ruby star medallion
                DrawRubyStarMedallion(new Vector2(side * 440f, BeamYOffset));

                // Terminal eyelet, spire crown and support flourish
                DrawTerminalFlourish(side);
            }
        }

        private void DrawArmDecorations(float side)
        {
            const int Steps = 16;
            var archPoints = new Vector2[Steps + 1];
            for (var i = 0; i <= Steps; i++)
            {
                var t = (float)i / Steps;
                var x = side * Mathf.Lerp(160f, 715f, t);
                var y = BeamYOffset + 4f + 26f * Mathf.Sin(t * Mathf.Pi);
                archPoints[i] = new Vector2(x, y);
            }

            // Draw lower arch
            DrawPolyline(archPoints, LineOutline, 7f, antialiased: true);
            DrawPolyline(archPoints, Gold, 4f, antialiased: true);
            DrawPolyline(archPoints, HighlightGold, 1.5f, antialiased: true);

            // Spindles connecting beam to arch
            var spindleT = new[] { 0.16f, 0.32f, 0.50f, 0.68f, 0.84f };
            foreach (var t in spindleT)
            {
                var x = side * Mathf.Lerp(160f, 715f, t);
                var archY = BeamYOffset + 4f + 26f * Mathf.Sin(t * Mathf.Pi);
                var top = new Vector2(x, BeamYOffset + 4f);
                var bot = new Vector2(x, archY - 1f);

                DrawLine(top, bot, LineOutline, 5f, antialiased: true);
                DrawLine(top, bot, Gold, 2.5f, antialiased: true);

                // Central spindle bead
                var mid = (top + bot) * 0.5f;
                DrawCircle(mid, 4f, LineOutline);
                DrawCircle(mid, 2.5f, Gold);
                DrawCircle(mid + new Vector2(-0.8f, -0.8f), 1f, HighlightGold);
            }

            // Inner filigree scroll bracket near the central crest
            var innerScrollBase = new Vector2(side * 175f, BeamYOffset + 6f);
            var innerScrollMid = new Vector2(side * 210f, BeamYOffset + 24f);
            var innerScrollTip = new Vector2(side * 230f, BeamYOffset + 14f);
            DrawPolyline([innerScrollBase, innerScrollMid, innerScrollTip], LineOutline, 5f, antialiased: true);
            DrawPolyline([innerScrollBase, innerScrollMid, innerScrollTip], Gold, 2.5f, antialiased: true);
            DrawCircle(innerScrollTip, 4.5f, LineOutline);
            DrawCircle(innerScrollTip, 2.8f, Gold);
        }

        private void DrawCuff(Vector2 pos)
        {
            var rectOutline = new Rect2(pos.X - 5f, pos.Y - 9f, 10f, 18f);
            var rectGold = new Rect2(pos.X - 3.5f, pos.Y - 7.5f, 7f, 15f);
            DrawRect(rectOutline, LineOutline, filled: true);
            DrawRect(rectGold, Gold, filled: true);
            DrawLine(new Vector2(pos.X - 1.5f, pos.Y - 6f), new Vector2(pos.X - 1.5f, pos.Y + 6f), HighlightGold, 1.5f);
        }

        private void DrawRubyStarMedallion(Vector2 center)
        {
            // 4-pointed star petal
            Vector2[] starOutline =
            [
                new(center.X, center.Y - 20f),
                new(center.X + 6f, center.Y - 6f),
                new(center.X + 20f, center.Y),
                new(center.X + 6f, center.Y + 6f),
                new(center.X, center.Y + 20f),
                new(center.X - 6f, center.Y + 6f),
                new(center.X - 20f, center.Y),
                new(center.X - 6f, center.Y - 6f)
            ];
            Vector2[] starCore =
            [
                new(center.X, center.Y - 17f),
                new(center.X + 4.5f, center.Y - 4.5f),
                new(center.X + 17f, center.Y),
                new(center.X + 4.5f, center.Y + 4.5f),
                new(center.X, center.Y + 17f),
                new(center.X - 4.5f, center.Y + 4.5f),
                new(center.X - 17f, center.Y),
                new(center.X - 4.5f, center.Y - 4.5f)
            ];
            DrawColoredPolygon(starOutline, LineOutline);
            DrawColoredPolygon(starCore, Gold);

            // Center ruby gem
            DrawCircle(center, 8f, LineOutline);
            DrawCircle(center, 6.5f, Gold);
            DrawCircle(center, 5f, RubyDark);
            DrawCircle(center, 3.8f, RubyRed);
            DrawCircle(center + new Vector2(-1.2f, -1.2f), 1.2f, RubyGlint);
        }

        private void DrawTerminalFlourish(float side)
        {
            var eyeletPos = new Vector2(side * BeamHalfSpan, BeamYOffset);

            // Upper spire crown
            Vector2[] crownOutline =
            [
                new(eyeletPos.X, eyeletPos.Y - 22f),
                new(eyeletPos.X + 6f, eyeletPos.Y - 10f),
                new(eyeletPos.X, eyeletPos.Y - 8f),
                new(eyeletPos.X - 6f, eyeletPos.Y - 10f)
            ];
            Vector2[] crownGold =
            [
                new(eyeletPos.X, eyeletPos.Y - 19f),
                new(eyeletPos.X + 4f, eyeletPos.Y - 10f),
                new(eyeletPos.X, eyeletPos.Y - 8.5f),
                new(eyeletPos.X - 4f, eyeletPos.Y - 10f)
            ];
            DrawColoredPolygon(crownOutline, LineOutline);
            DrawColoredPolygon(crownGold, Gold);
            DrawLine(new Vector2(eyeletPos.X, eyeletPos.Y - 19f), new Vector2(eyeletPos.X, eyeletPos.Y - 9f), HighlightGold, 1.5f);

            // Lower support bracket curving from arch into eyelet
            var bracketBase = new Vector2(side * (BeamHalfSpan - 24f), BeamYOffset + 4f);
            var bracketDrop = new Vector2(side * (BeamHalfSpan - 10f), BeamYOffset + 18f);
            var bracketEnd = new Vector2(eyeletPos.X, BeamYOffset + 14f);
            DrawPolyline([bracketBase, bracketDrop, bracketEnd], LineOutline, 5f, antialiased: true);
            DrawPolyline([bracketBase, bracketDrop, bracketEnd], Gold, 2.5f, antialiased: true);

            // Forged Eyelet Ring
            DrawCircle(eyeletPos, 13f, LineOutline);
            DrawCircle(eyeletPos, 9.5f, Gold);
            DrawArc(eyeletPos, 9.5f, -Mathf.Pi * 0.75f, -Mathf.Pi * 0.25f, 8, HighlightGold, 1.8f, antialiased: true);
            DrawCircle(eyeletPos, 5f, LineOutline);
            DrawCircle(eyeletPos, 3f, new Color("362217"));
        }
    }

    private static void ApplyIdle(NCreature? pan, float rotation)
    {
        if (pan?.Entity.IsAlive == true)
            pan.Body.Rotation = rotation;
    }

    private void SetLineVisible(string side, bool visible)
    {
        var (outline, core) = side == "LEFT"
            ? (_leftOutline, _leftCore)
            : (_rightOutline, _rightCore);
        if (outline is not null)
            outline.Visible = visible;
        if (core is not null)
            core.Visible = visible;
    }

    private void SetLineModulate(string? side, Color color)
    {
        if (side is null)
            return;
        var (outline, core) = side == "LEFT"
            ? (_leftOutline, _leftCore)
            : (_rightOutline, _rightCore);
        if (outline is not null)
            outline.Modulate = color;
        if (core is not null)
            core.Modulate = color;
    }

    private void OnPanDied(Creature _)
    {
        RefreshDeathVisibility();
        if (_left?.Entity.IsAlive != true && _right?.Entity.IsAlive != true && !_assemblyFading)
            TaskHelper.RunSafely(FadeAssemblyAsync());
    }

    private async Task FadeAssemblyAsync()
    {
        _assemblyFading = true;
        if (_central is null)
            return;
        try
        {
            KillTween(ref _pulseTween);
            _pulseTween = CreateTween();
            _pulseTween.TweenProperty(_central, "modulate:a", 0f, 0.18f);
            await AwaitTweenAsync(_pulseTween);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Warn($"Libra final assembly exit failed open: {ex.Message}");
        }
        finally
        {
            if (_central is not null)
                _central.Visible = false;
        }
    }

    private void AbortPendingPresentation()
    {
        _presentationGeneration++;
        KillTween(ref _motionTween);
        KillTween(ref _pulseTween);
        _pending = Task.CompletedTask;
        _cueActive = false;
        SnapToCurrentState();
    }
    private void OnCombatEnded(CombatRoom _) => Cleanup();
    private void OnTreeExiting() => Cleanup();

    private void Cleanup()
    {
        if (_disposed)
            return;
        _disposed = true;
        _presentationGeneration++;
        TreeExiting -= OnTreeExiting;
        if (_leftPower is not null)
            _leftPower.PresentationChanged -= OnPresentationChanged;
        if (_rightPower is not null)
            _rightPower.PresentationChanged -= OnPresentationChanged;
        if (_left is not null)
        {
            _left.Entity.Died -= OnPanDied;
            Sessions.Remove(_left.Entity);
        }
        if (_right is not null)
        {
            _right.Entity.Died -= OnPanDied;
            Sessions.Remove(_right.Entity);
        }
        if (_bound)
            CombatManager.Instance.CombatEnded -= OnCombatEnded;
        KillTween(ref _motionTween);
        KillTween(ref _pulseTween);
    }

    private static void KillTween(ref Tween? tween)
    {
        if (tween is { } current && current.IsValid())
            current.Kill();
        tween = null;
    }
}
