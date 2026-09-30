using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using SakuraMod.SakuraModCode.Character;
using STS2RitsuLib.Audio;

namespace SakuraMod.SakuraModCode.Cards;

/// <summary>
/// Song's rhythm: how long each beat waits, which pitch it sings, and whether it
/// still writes a note onto the staff.
/// </summary>
/// <remarks>
/// Kept as plain static functions so the contract suite can pin the schedule
/// without a scene. The first eight beats are quarter notes, the next eight
/// faster notes — the song speeds up rather than being cut off — and past that a
/// beat no longer waits at all, so a huge play cannot turn into a cutscene. The
/// same cap is Arrow's answer to the same problem.
/// <para>
/// Gaps are relative to the previous beat, not to a fixed start. Sakura's Song
/// gains Block after every hit and the engine waits about 0.25 s for that; a gap
/// measured from the previous beat absorbs that wait inside a 0.3 s quarter note
/// instead of letting it push every later beat off the grid.
/// </para>
/// </remarks>
internal static class SongBeatSchedule
{
    internal const int SlowBeats = 8;
    internal const int DrawnBeats = 16;
    internal const float SlowGap = 0.30f;
    internal const float FastGap = 0.15f;
    internal const int PitchCount = 10;

    /// <summary>
    /// Staff step of each pitch: 0 is the bottom line, one step is half a staff
    /// space. Rises through the staff and tops out in the space above it, so no
    /// note needs a ledger line.
    /// </summary>
    private static readonly int[] StaffSteps = [-1, 0, 1, 2, 3, 5, 6, 7, 8, 9];

    /// <summary>Seconds beat <paramref name="index"/> waits after the previous one.</summary>
    /// <remarks>Beat 0 measures from the moment its note leaves the clef.</remarks>
    internal static float GapBefore(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        return index < SlowBeats ? SlowGap : index < DrawnBeats ? FastGap : 0f;
    }

    /// <summary>
    /// Pitch of beat <paramref name="index"/>: the scale rises through both octaves,
    /// then keeps cycling the upper octave.
    /// </summary>
    /// <remarks>
    /// Holding the top pitch instead turned the fast eighth-note run into one note
    /// repeated seven times, drawn as a flat row above the staff — the climax of
    /// the song standing still.
    /// </remarks>
    internal static int PitchIndex(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        const int upperOctave = PitchCount / 2;
        return index < PitchCount ? index : upperOctave + (index - PitchCount) % upperOctave;
    }

    internal static int StaffStep(int index) => StaffSteps[PitchIndex(index)];

    internal static bool DrawsNote(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        return index < DrawnBeats;
    }

    internal static bool IsFast(int index) => index >= SlowBeats;

    /// <summary>
    /// Horizontal share of the gap before note <paramref name="index"/>, as in
    /// engraving where a longer note takes more room. Even spacing crowded the
    /// slow half of a 16-beat play as badly as the fast half.
    /// </summary>
    internal static float SpacingWeight(int index) => IsFast(index) ? 0.62f : 1f;

    /// <summary>Sum of <see cref="SpacingWeight"/> over the gaps between <paramref name="drawn"/> notes.</summary>
    internal static float SpacingUnits(int drawn)
    {
        var units = 0f;
        for (var i = 1; i < drawn; i++)
            units += SpacingWeight(i);
        return units;
    }

    /// <summary>
    /// Written rhythm while the song is slow: mostly quarters, broken up by an
    /// eighth, a half, and a dotted quarter so the line reads as a phrase.
    /// </summary>
    private static readonly SongGlyph[] SlowRhythm =
    [
        SongGlyph.Quarter, SongGlyph.Quarter, SongGlyph.Half, SongGlyph.Eighth,
        SongGlyph.Quarter, SongGlyph.DottedQuarter, SongGlyph.Eighth, SongGlyph.Half
    ];

    /// <summary>Written size of a note once the song speeds up.</summary>
    /// <remarks>
    /// Sixteen notes over an enemy line leave about 44px between heads. At full
    /// size, sixteenth-note double flags and down stems overlapped their
    /// neighbours in the 16-beat render and the run stopped reading as notes; the
    /// fast run is therefore plain eighths, drawn smaller.
    /// </remarks>
    internal const float FastNoteSize = 0.85f;

    /// <summary>
    /// The note written for beat <paramref name="index"/> of a <paramref name="beats"/>-beat
    /// song. The closing beat is a whole note: an open head with no stem reads as
    /// the end of the phrase.
    /// </summary>
    internal static SongGlyph GlyphFor(int index, int beats)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        if (index == beats - 1)
            return SongGlyph.Whole;
        return IsFast(index) ? SongGlyph.Eighth : SlowRhythm[index];
    }

    /// <summary>
    /// Engraving rule: a head on or above the middle line (step 4) takes its stem
    /// down, below it up.
    /// </summary>
    internal static bool StemDown(int index) => StaffStep(index) >= 4;

    /// <summary>
    /// Expands the exhausted cards into beats, in exhaust order: each card's first
    /// hit is a plain beat and any further hits are its echo, right behind it.
    /// Beats that belong to no card (Clow's activated extra hits) close the song.
    /// </summary>
    /// <remarks>
    /// Only the Voice card's Echo gives a card more than one hit, so the echo
    /// flag is what lets the staff show the Voice card paying off. The total is
    /// exactly the hit count gameplay already used.
    /// </remarks>
    internal static bool[] EchoBeats(IEnumerable<int> hitsPerCard, int trailingPlainBeats = 0)
    {
        ArgumentNullException.ThrowIfNull(hitsPerCard);
        ArgumentOutOfRangeException.ThrowIfNegative(trailingPlainBeats);
        var beats = new List<bool>();
        foreach (var hits in hitsPerCard)
        {
            for (var i = 0; i < hits; i++)
                beats.Add(i > 0);
        }
        for (var i = 0; i < trailingPlainBeats; i++)
            beats.Add(false);
        return [.. beats];
    }

    /// <summary>Total waited time for a play of <paramref name="beats"/> beats.</summary>
    internal static float TotalWait(int beats)
    {
        var total = 0f;
        for (var i = 0; i < beats; i++)
            total += GapBefore(i);
        return total;
    }
}

/// <summary>A written note value; each maps to one or two cells of the glyph atlas.</summary>
internal enum SongGlyph
{
    Quarter,
    Eighth,
    Half,
    DottedQuarter,
    Whole
}

/// <summary>The instrument a Song play sings with.</summary>
internal enum SongTimbre
{
    /// <summary>Clow's Song: a plucked harp.</summary>
    Harp,

    /// <summary>Sakura's Song: a music box.</summary>
    MusicBox
}

/// <summary>
/// Song's staff: a treble clef lights behind Sakura, a five-line staff flows from
/// it like the Song spirit's hair to the air above the enemies, and each hit
/// writes one rising note onto that staff while a sound ring passes through every
/// enemy the hit strikes.
/// </summary>
/// <remarks>
/// The card knows how many beats the play has before the session opens — the
/// cards are exhausted first — so the note slots are laid out once, evenly across
/// the enemy line. Each note is launched at the previous beat and flies for one
/// beat gap, so it arrives exactly on the grid even while gameplay spends part
/// of that gap gaining Block.
/// <para>
/// <see cref="Cues.Beat"/> is awaited before each hit's damage, the same shape as
/// Arrow's <c>Loose</c>: the landing bounce, the note's pitch, and the damage
/// numbers happen on one frame. A session that is not running returns at once, so
/// players with card VFX off never wait for a song nobody drew.
/// </para>
/// </remarks>
internal sealed class SongStaffVfx : CelVfxSession
{
    internal const string ScenePath =
        MainFile.ResPath + "/scenes/combat/card_vfx/song_staff_vfx.tscn";
    internal const string StaffShaderPath =
        MainFile.ResPath + "/shaders/card_vfx/song_staff.gdshader";
    internal const string GlyphShaderPath =
        MainFile.ResPath + "/shaders/card_vfx/song_glyph.gdshader";
    internal const string GlyphAtlasPath =
        MainFile.ResPath + "/images/card_vfx/song/glyphs_sdf.png";
    internal const string SfxRoot = MainFile.ResPath + "/sfx/song";

    /// <summary>
    /// The scene owns its shaders and the glyph atlas as ext resources, so the
    /// scene is the only root to preload. The notes are played from their files
    /// by the audio service rather than read from the resource cache.
    /// </summary>
    internal static IReadOnlyList<string> AssetPaths { get; } = [ScenePath];

    /// <summary>On-screen pixels between two staff lines.</summary>
    /// <remarks>
    /// Measured on the glyph mock: at 20px the note stems and the clef's
    /// hairlines were thinner than the ink and read as dark wire; 26px keeps a
    /// visible white fill in every stroke while a five-note staff still fits
    /// above a small enemy.
    /// </remarks>
    internal const float StaffSpace = 26f;

    // Atlas layout from SakuraMod/images/card_vfx/song/glyphs_sdf.json; the
    // contract suite checks these against the JSON so a re-bake cannot drift.
    internal const float AtlasStaffSpace = 40f;
    internal static readonly Vector2 AtlasCell = new(144f, 353f);
    internal static readonly Vector2 AtlasOrigin = new(18f, 193.75f);
    internal const int ClefCell = 0;
    internal const int AtlasCellCount = 10;

    /// <summary>Atlas cell of a note glyph, following the order the bake script writes.</summary>
    internal static int CellFor(SongGlyph glyph, bool stemDown) => glyph switch
    {
        SongGlyph.Quarter => stemDown ? 2 : 1,
        SongGlyph.Eighth => stemDown ? 4 : 3,
        SongGlyph.Half => stemDown ? 6 : 5,
        SongGlyph.DottedQuarter => stemDown ? 8 : 7,
        SongGlyph.Whole => 9,
        _ => throw new ArgumentOutOfRangeException(nameof(glyph), glyph, null)
    };
    private const float GlyphScale = StaffSpace / AtlasStaffSpace;

    private const float ClefFormDuration = 0.18f;
    private const float StaffWriteDuration = 0.32f;
    private const float LandingBounceDuration = 0.12f;
    private const float RippleDuration = 0.30f;
    private const float FinaleFlashDuration = 2f / StepFrequency;
    private const float FinaleScatterDuration = 0.6f;
    private const float FadeDuration = 0.2f;

    /// <summary>How long after an echo beat's note its quieter repeat sounds.</summary>
    private const float EchoDelay = 0.09f;
    private const float EchoGain = 0.45f;
    private const float EchoDuration = 0.36f;

    /// <summary>Clearance between the staff's bottom line and the tallest enemy.</summary>
    private const float ZoneClearance = 34f;

    /// <summary>Screen margin kept above the staff's top line for the top bar.</summary>
    private const float TopMargin = 118f;

    /// <summary>Widest spacing between two written notes.</summary>
    private const float MaxNoteSpacing = StaffSpace * 3.4f;

    /// <summary>
    /// Spacing a slow note wants. When the enemy line is narrower than the song
    /// needs at this spacing, the landing staff extends back toward the clef.
    /// </summary>
    private const float IdealNoteSpacing = StaffSpace * 2.6f;

    /// <summary>Clear run the flowing staff keeps between the clef and the landing part.</summary>
    private const float MinFlowingRun = 220f;

    private const float SlotPadding = 28f;

    /// <summary>
    /// Safety net, not a timer. Sixteen written beats wait 3.6 s, plus prelude and
    /// finale; Sakura's Block waits can stretch that further, and if a very long
    /// play outlives the net the session simply stops drawing while gameplay
    /// continues.
    /// </summary>
    protected override float MaximumLifetime => 8.0f;

    private const int VfxZIndex = 3000;

    private static readonly Color LineColour = new(0.80f, 0.96f, 0.87f);
    private static readonly Color NoteFill = new(0.98f, 1f, 0.98f);
    private static readonly Color RippleColour = new(0.78f, 0.97f, 0.88f, 0.9f);

    private static bool _loadFailureLogged;
    private static bool _audioFailureLogged;

    private readonly ColorRect _staff;
    private readonly ShaderMaterial _staffMaterial;
    private readonly Node2D _glyphs;
    private readonly Node2D _ripples;
    private readonly Sprite2D _template;
    private readonly SongTimbre _timbre;
    private readonly Color _accent;
    private readonly int _beats;
    private readonly bool[] _echoes;
    private readonly float _direction;
    private readonly Vector2 _clefOrigin;
    private readonly Vector2 _launchPoint;
    private readonly Vector2[] _slots;
    private readonly Dictionary<int, NoteVisual> _notes = [];
    private NoteVisual? _clef;
    private ulong _lastBeatMicros;
    private bool _faded;

    private SongStaffVfx(
        Node2D root,
        NCombatRoom room,
        CardModel card,
        Creature? caster,
        IReadOnlyList<Creature> targets,
        bool[] echoes)
        : base(root, room)
    {
        var beats = echoes.Length;
        _staff = root.GetNode<ColorRect>("%Staff");
        _glyphs = root.GetNode<Node2D>("%Glyphs");
        _ripples = root.GetNode<Node2D>("%Ripples");
        _template = root.GetNode<Sprite2D>("%GlyphTemplate");
        _staffMaterial = CelVfxGeometry.DuplicateMaterial(_staff, "song staff");
        _beats = beats;
        _echoes = echoes;

        _timbre = SakuraCardCatalog.TryGetMetadata(card, out var metadata)
            && metadata.Era == SourceEraClass.Sakura
                ? SongTimbre.MusicBox
                : SongTimbre.Harp;
        _accent = MagicCircleInkColour(card) ?? new Color(0.97f, 0.83f, 0.45f);

        var layout = Layout.Resolve(room, caster, targets, beats);
        _direction = layout.Direction;
        _clefOrigin = layout.ClefOrigin;
        _launchPoint = layout.LaunchPoint;
        _slots = layout.Slots;
        ApplyStaff(layout);
    }

    protected override IEnumerable<ShaderMaterial> Materials => [_staffMaterial];

    private bool IsDrawing => IsActive();

    internal static Task PlayOrResolveAsync(
        CardModel card,
        Creature? caster,
        IReadOnlyList<Creature> targets,
        bool[] echoes,
        Func<Cues, Task> resolveGameplay)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(echoes);
        ArgumentNullException.ThrowIfNull(resolveGameplay);

        return CelVfxSession.PlayOrResolveAsync(
            "Song staff",
            () => echoes.Length > 0 && targets.Count > 0 ? TryCreate(card, caster, targets, echoes) : null,
            session => session.PlayPrelude(card, caster),
            scope => resolveGameplay(new Cues(scope)),
            session => session.FadeAndDispose(),
            session => session.Dispose());
    }

    internal sealed class Cues(CueScope<SongStaffVfx> scope)
    {
        /// <summary>Whether the staff is being drawn; false means use the baseline hit feedback.</summary>
        /// <remarks>
        /// Asks the session too: a very long Sakura Song can outlive the lifetime
        /// net, and the hits after that must fall back rather than land unseen.
        /// </remarks>
        internal bool IsLive => scope.IsLiveWhere(static session => session.IsDrawing);

        /// <summary>
        /// Waits for beat <paramref name="index"/> on the song's grid, then lands
        /// its note, sings its pitch, and rings through <paramref name="targets"/>.
        /// Awaited before that beat's damage.
        /// </summary>
        internal Task Beat(int index, IReadOnlyList<Creature> targets)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentNullException.ThrowIfNull(targets);
            return scope.InvokeAsync("beat", session => session.BeatAsync(index, targets));
        }

        /// <summary>The closing chord: the staff flashes the era colour and the notes scatter.</summary>
        internal void Finale() => scope.Invoke("finale", session => session.Finale());
    }

    internal static string NotePath(SongTimbre timbre, int pitch) =>
        $"{SfxRoot}/{TimbreFolder(timbre)}/note_{Math.Clamp(pitch, 0, SongBeatSchedule.PitchCount - 1):00}.ogg";

    internal static string ChordPath(SongTimbre timbre) => $"{SfxRoot}/{TimbreFolder(timbre)}/chord.ogg";

    private static string TimbreFolder(SongTimbre timbre) => timbre == SongTimbre.MusicBox ? "musicbox" : "harp";

    private static SongStaffVfx? TryCreate(
        CardModel card,
        Creature? caster,
        IReadOnlyList<Creature> targets,
        bool[] echoes)
    {
        if (!TryPrepare("Song staff", LoadScene, out var room, out _, out var scene))
            return null;

        Node2D? root = null;
        try
        {
            root = scene.Instantiate<Node2D>();
            root.Name = "SakuraSongStaffVfx";
            root.ZAsRelative = false;
            root.ZIndex = VfxZIndex;
            room.CombatVfxContainer.AddChildSafely(root);

            var session = new SongStaffVfx(root, room, card, caster, targets, echoes);
            // Started after construction: the base clock reads Materials.
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

    /// <summary>
    /// Shared wand tap and speed lines, then the clef lights and the staff is
    /// written toward the enemies while the first note leaves the clef.
    /// </summary>
    /// <remarks>
    /// Returns without waiting for the first note: the first beat's gap is
    /// measured from its launch, so the gameplay callback starts at once and
    /// <see cref="Cues.Beat"/> holds the first hit until the note arrives.
    /// </remarks>
    private async Task<bool> PlayPrelude(CardModel card, Creature? caster)
    {
        if (!await PlayCelPrelude(card, caster))
            return false;

        _clef = CreateGlyph(ClefCell, "SongClef");
        _clef.Place(_clefOrigin);
        Track(_clef.CreateFormTween(ClefFormDuration));

        var write = Track(Root.CreateTween());
        write.TweenMethod(
                Callable.From<float>(value => _staffMaterial.SetShaderParameter("reveal", value)),
                0f,
                1f,
                StaffWriteDuration)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);

        Launch(0);
        _lastBeatMicros = Godot.Time.GetTicksUsec();
        return IsActive();
    }

    private async Task BeatAsync(int index, IReadOnlyList<Creature> targets)
    {
        if (!IsActive())
            return;

        var remaining = SongBeatSchedule.GapBefore(index) - SecondsSince(_lastBeatMicros);
        if (remaining > 0f && !await WaitActive(remaining))
            return;

        _lastBeatMicros = Godot.Time.GetTicksUsec();
        Land(index);
        foreach (var target in targets)
        {
            if (target.IsAlive)
                SpawnRipple(target, index);
        }

        if (index + 1 < _beats)
            Launch(index + 1);
    }

    /// <summary>
    /// Sends note <paramref name="index"/> from the clef to its slot, timed to
    /// arrive on its beat.
    /// </summary>
    private void Launch(int index)
    {
        if (!IsActive() || !SongBeatSchedule.DrawsNote(index) || index >= _slots.Length)
            return;

        var note = CreateGlyph(NoteCell(index), $"SongNote{index + 1}", NoteSize(index));
        _notes[index] = note;
        var flight = SongBeatSchedule.GapBefore(index) * 0.92f;
        Track(note.CreateFlightTween(_launchPoint, SlotFor(index), flight));
    }

    private int NoteCell(int index) =>
        CellFor(SongBeatSchedule.GlyphFor(index, _beats), SongBeatSchedule.StemDown(index));

    private static float NoteSize(int index) =>
        SongBeatSchedule.IsFast(index) ? SongBeatSchedule.FastNoteSize : 1f;

    private void Land(int index)
    {
        var closing = index == _beats - 1;
        var echo = _echoes[index];
        if (_notes.TryGetValue(index, out var note))
        {
            if (closing)
                note.SetAccent(1f);
            Track(note.CreateLandingTween(SlotFor(index), LandingBounceDuration));
            if (echo)
                SpawnEchoGhost(index);
        }

        var path = NotePath(_timbre, SongBeatSchedule.PitchIndex(index));
        var gain = SongBeatSchedule.IsFast(index) ? 0.78f : 0.92f;
        PlaySound(path, gain);
        if (echo)
        {
            var repeat = Track(Root.CreateTween());
            repeat.TweenInterval(EchoDelay);
            repeat.TweenCallback(Callable.From(() => PlaySound(path, gain * EchoGain)));
        }
    }

    /// <summary>
    /// The Voice card's Echo, made visible: a faint copy of the note that lands
    /// beside it and drifts on, the way a sound repeats and fades.
    /// </summary>
    private void SpawnEchoGhost(int index)
    {
        var ghost = CreateGlyph(NoteCell(index), $"SongEcho{index + 1}", NoteSize(index));
        var from = SlotFor(index) + new Vector2(_direction * 0.55f * StaffSpace, -0.4f * StaffSpace);
        var drift = new Vector2(_direction * 0.6f * StaffSpace, -0.25f * StaffSpace);
        Track(ghost.CreateEchoTween(from, drift, EchoDelay, EchoDuration));
    }

    private void Finale()
    {
        if (!IsActive())
            return;

        PlaySound(ChordPath(_timbre), 0.9f);
        BeginHold();

        var flash = Track(Root.CreateTween());
        flash.TweenMethod(
            Callable.From<float>(value => _staffMaterial.SetShaderParameter("flash", value)),
            1f,
            0.35f,
            FinaleScatterDuration);
        _clef?.SetAccent(1f);

        var scatter = Track(Root.CreateTween().SetParallel());
        var order = 0;
        foreach (var (index, note) in _notes.OrderBy(static pair => pair.Key))
        {
            var drift = new Vector2(_direction * (18f + index % 3 * 10f), -(70f + index % 4 * 18f));
            note.AddScatter(scatter, drift, FinaleFlashDuration + order * 0.025f, FinaleScatterDuration);
            order++;
        }
    }

    private void FadeAndDispose()
    {
        if (_faded || !IsActive())
        {
            Dispose();
            return;
        }

        _faded = true;
        var fade = Track(Root.CreateTween());
        fade.TweenInterval(FinaleScatterDuration * 0.7f);
        fade.TweenProperty(Root, "modulate:a", 0f, FadeDuration);
        fade.TweenCallback(Callable.From(Dispose));
    }

    private void SpawnRipple(Creature target, int index)
    {
        if (Room.GetCreatureNode(target) is not { } node || !GodotObject.IsInstanceValid(node))
            return;

        var centre = node.VfxSpawnPosition;
        var tween = Track(Root.CreateTween().SetParallel());
        // One ring once the song stops waiting; two while it still has time to be heard.
        var rings = SongBeatSchedule.DrawsNote(index) ? 2 : 1;
        for (var ring = 0; ring < rings; ring++)
        {
            var line = CreateRing(centre);
            var delay = ring * 0.07f;
            tween.TweenProperty(line, "scale", Vector2.One * (1.55f + ring * 0.25f), RippleDuration)
                .From(Vector2.One * 0.45f)
                .SetDelay(delay)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Cubic);
            tween.TweenProperty(line, "modulate:a", 0f, RippleDuration)
                .From(1f)
                .SetDelay(delay)
                .SetEase(Tween.EaseType.In)
                .SetTrans(Tween.TransitionType.Quad);
            tween.TweenCallback(Callable.From(line.QueueFreeSafely)).SetDelay(delay + RippleDuration);
        }
    }

    private Line2D CreateRing(Vector2 centre)
    {
        const int segments = 40;
        const float radius = 34f;
        var points = new Vector2[segments + 1];
        for (var i = 0; i <= segments; i++)
        {
            var angle = Mathf.Tau * i / segments;
            points[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.72f) * radius;
        }

        var line = new Line2D
        {
            Name = "SongRipple",
            Points = points,
            Width = 3f,
            DefaultColor = RippleColour,
            Antialiased = true,
            GlobalPosition = centre
        };
        _ripples.AddChildSafely(line);
        line.GlobalPosition = centre;
        return line;
    }

    private void ApplyStaff(Layout layout)
    {
        _staff.Size = layout.RegionSize;
        _staff.GlobalPosition = layout.RegionOrigin;
        _staffMaterial.SetShaderParameter("region_size", layout.RegionSize);
        _staffMaterial.SetShaderParameter("clef_point", layout.StaffStart - layout.RegionOrigin);
        _staffMaterial.SetShaderParameter("zone_start", layout.ZoneStart - layout.RegionOrigin.X);
        _staffMaterial.SetShaderParameter("zone_end", layout.ZoneEnd - layout.RegionOrigin.X);
        _staffMaterial.SetShaderParameter("zone_mid_y", layout.ZoneMidY - layout.RegionOrigin.Y);
        _staffMaterial.SetShaderParameter("staff_space", StaffSpace);
        _staffMaterial.SetShaderParameter("reveal", 0f);
        _staffMaterial.SetShaderParameter("flash", 0f);
        _staffMaterial.SetShaderParameter("line_colour", LineColour);
        _staffMaterial.SetShaderParameter("accent_colour", _accent);
    }

    private Vector2 SlotFor(int index) => _slots[Math.Min(index, _slots.Length - 1)];

    private NoteVisual CreateGlyph(int cell, string name, float size = 1f)
    {
        var sprite = (Sprite2D)_template.Duplicate();
        sprite.Name = name;
        sprite.Visible = true;
        _glyphs.AddChildSafely(sprite);
        var material = CelVfxGeometry.DuplicateMaterial(sprite, name);
        sprite.RegionRect = new Rect2(new Vector2(AtlasCell.X * cell, 0f), AtlasCell);
        sprite.Offset = -AtlasOrigin;
        material.SetShaderParameter("fill_colour", NoteFill);
        material.SetShaderParameter("accent_colour", _accent);
        material.SetShaderParameter("accent", 0f);
        material.SetShaderParameter("opacity", 0f);
        return new NoteVisual(sprite, material, GlyphScale * size);
    }

    private void PlaySound(string path, float gain)
    {
        try
        {
            var result = GameAudioService.Shared.PlayOneShot(
                new ResourceSoundFileSource(path),
                new AudioPlaybackOptions
                {
                    Volume = gain * SakuraGameVolumeFollower.VoiceFactor(),
                    Scope = AudioLifecycleScope.Combat,
                    DebugName = "Song.note"
                });
            if (!result.Succeeded && !_audioFailureLogged)
            {
                _audioFailureLogged = true;
                MainFile.Logger.Warn($"Song note {path} failed: {result.Status} {result.Message}");
            }
        }
        catch (Exception exception)
        {
            if (_audioFailureLogged)
                return;
            _audioFailureLogged = true;
            MainFile.Logger.Warn($"Song note {path} failed: {exception}");
        }
    }

    private static float SecondsSince(ulong micros) => (Godot.Time.GetTicksUsec() - micros) / 1_000_000f;

    private static PackedScene LoadScene() => PreloadManager.Cache.GetScene(ScenePath);

    private static void LogLoadFailure(Exception exception)
    {
        if (_loadFailureLogged)
            return;

        _loadFailureLogged = true;
        MainFile.Logger.Error($"Could not create Song staff VFX from {ScenePath}: {exception}");
    }

    /// <summary>Where everything sits, resolved once from the caster and the enemy line.</summary>
    private readonly record struct Layout(
        float Direction,
        Vector2 ClefOrigin,
        Vector2 StaffStart,
        Vector2 LaunchPoint,
        float ZoneStart,
        float ZoneEnd,
        float ZoneMidY,
        Vector2[] Slots,
        Vector2 RegionOrigin,
        Vector2 RegionSize)
    {
        internal static Layout Resolve(
            NCombatRoom room,
            Creature? caster,
            IReadOnlyList<Creature> targets,
            int beats)
        {
            var viewport = room.CombatVfxContainer.GetViewportRect();
            var anchor = caster is null ? null : CelVfxGeometry.ResolveCaster(room.GetCreatureNode(caster));
            var body = anchor?.BodyCenter ?? viewport.GetCenter() + new Vector2(-viewport.Size.X * 0.25f, 0f);
            var bodySize = anchor?.BodySize ?? new Vector2(120f, 260f);
            var facing = anchor?.FacingSign ?? 1f;

            var enemies = EnemyBounds(room, targets, viewport);
            var direction = enemies.GetCenter().X >= body.X ? 1f : -1f;

            // Middle staff line at the clef: behind and above Sakura's head. The
            // effect draws over creatures, and a clef or staff across her body
            // covered her face in the first render; above her head the staff
            // reads as rising from her instead.
            var bodyTop = body.Y - bodySize.Y * 0.5f;
            var clefMid = new Vector2(
                body.X - facing * (bodySize.X * 0.5f + 1.2f * StaffSpace),
                Math.Max(bodyTop - 2.5f * StaffSpace, viewport.Position.Y + TopMargin + 2f * StaffSpace));

            // Level landing staff just above the tallest enemy, clamped under the top bar.
            var zoneMidY = enemies.Position.Y - ZoneClearance - 2f * StaffSpace;
            zoneMidY = Math.Max(zoneMidY, viewport.Position.Y + TopMargin + 2f * StaffSpace);

            var nearEdge = direction > 0f ? enemies.Position.X : enemies.End.X;
            var farEdge = direction > 0f ? enemies.End.X : enemies.Position.X;
            var zoneStart = nearEdge - direction * 24f;
            var zoneEnd = farEdge + direction * 24f;
            // Room the song needs; a narrow enemy line gives the notes more staff
            // by extending the landing part back toward the clef.
            var drawn = Math.Clamp(beats, 1, SongBeatSchedule.DrawnBeats);
            var needed = SongBeatSchedule.SpacingUnits(drawn) * IdealNoteSpacing + 2f * SlotPadding;
            if ((zoneEnd - zoneStart) * direction < needed)
                zoneStart = zoneEnd - direction * needed;
            // A staff has to travel before it lands, or the flowing part disappears.
            if ((zoneStart - clefMid.X) * direction < MinFlowingRun)
                zoneStart = clefMid.X + direction * MinFlowingRun;
            if ((zoneEnd - zoneStart) * direction < 4f * StaffSpace)
                zoneEnd = zoneStart + direction * 4f * StaffSpace;

            // The clef's own origin is its G line, one staff space under the middle
            // line. Its glyph extends toward +X, so for a staff running toward -X the
            // clef is shifted to stay on the staff's starting end.
            var clefOrigin = new Vector2(
                direction > 0f ? clefMid.X : clefMid.X - 2.6f * StaffSpace,
                clefMid.Y + StaffSpace);
            var staffStart = new Vector2(clefMid.X - direction * 0.6f * StaffSpace, clefMid.Y);
            var launchPoint = new Vector2(clefMid.X + direction * 3.2f * StaffSpace, clefMid.Y);

            var slots = NoteSlots(zoneStart, zoneEnd, zoneMidY, direction, beats);

            var left = Math.Min(staffStart.X, zoneEnd) - 40f;
            var right = Math.Max(staffStart.X, zoneEnd) + 40f;
            var top = Math.Min(clefMid.Y, zoneMidY) - 4f * StaffSpace - 32f;
            var bottom = Math.Max(clefMid.Y, zoneMidY) + 4f * StaffSpace + 32f;
            return new Layout(
                direction,
                clefOrigin,
                staffStart,
                launchPoint,
                zoneStart,
                zoneEnd,
                zoneMidY,
                slots,
                new Vector2(left, top),
                new Vector2(right - left, bottom - top));
        }

        /// <summary>
        /// Evenly spaced slots across the landing staff, centred as a group so a
        /// short song does not leave its notes stranded at one end.
        /// </summary>
        private static Vector2[] NoteSlots(float zoneStart, float zoneEnd, float zoneMidY, float direction, int beats)
        {
            var drawn = Math.Clamp(beats, 1, SongBeatSchedule.DrawnBeats);
            var usable = Math.Max(0f, Math.Abs(zoneEnd - zoneStart) - 2f * SlotPadding);
            var units = SongBeatSchedule.SpacingUnits(drawn);
            var unit = units > 0f ? Math.Min(usable / units, MaxNoteSpacing) : 0f;
            var group = unit * units;
            var x = zoneStart + direction * (SlotPadding + (usable - group) * 0.5f);
            var slots = new Vector2[drawn];
            var bottomLine = zoneMidY + 2f * StaffSpace;
            for (var i = 0; i < drawn; i++)
            {
                if (i > 0)
                    x += direction * unit * SongBeatSchedule.SpacingWeight(i);
                var y = bottomLine - SongBeatSchedule.StaffStep(i) * StaffSpace * 0.5f;
                slots[i] = new Vector2(x, y);
            }
            return slots;
        }

        private static Rect2 EnemyBounds(NCombatRoom room, IReadOnlyList<Creature> targets, Rect2 viewport)
        {
            Rect2? bounds = null;
            foreach (var target in targets)
            {
                if (room.GetCreatureNode(target) is not { } node
                    || !GodotObject.IsInstanceValid(node)
                    || node.Hitbox is not { } hitbox
                    || !GodotObject.IsInstanceValid(hitbox))
                    continue;

                var rect = hitbox.GetGlobalRect();
                if (rect.Size.X <= 1f || rect.Size.Y <= 1f)
                    continue;
                bounds = bounds is { } existing ? existing.Merge(rect) : rect;
            }

            return bounds ?? new Rect2(
                viewport.Position + new Vector2(viewport.Size.X * 0.58f, viewport.Size.Y * 0.40f),
                new Vector2(viewport.Size.X * 0.28f, viewport.Size.Y * 0.30f));
        }
    }

    /// <summary>One clef or note glyph and its own material.</summary>
    private sealed class NoteVisual(Sprite2D sprite, ShaderMaterial material, float scale)
    {
        private Tween? _motion;

        internal void Place(Vector2 position)
        {
            sprite.GlobalPosition = position;
            sprite.Scale = Vector2.One * scale;
        }

        internal void SetAccent(float value) => material.SetShaderParameter("accent", value);

        internal Tween CreateFormTween(float duration)
        {
            var tween = sprite.CreateTween().SetParallel();
            tween.TweenMethod(
                Callable.From<float>(value => material.SetShaderParameter("opacity", value)),
                0f,
                1f,
                duration);
            tween.TweenProperty(sprite, "scale", Vector2.One * scale, duration)
                .From(Vector2.One * scale * 0.7f)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Back);
            return StartMotion(tween);
        }

        /// <summary>
        /// Flies from the clef to the slot on a shallow arc, fading in as it
        /// leaves, and arrives slightly small so the landing can bounce it up.
        /// </summary>
        internal Tween CreateFlightTween(Vector2 from, Vector2 to, float duration)
        {
            sprite.GlobalPosition = from;
            sprite.Scale = Vector2.One * scale * 0.6f;
            var lift = Math.Min(60f, from.DistanceTo(to) * 0.08f);
            var tween = sprite.CreateTween();
            tween.TweenMethod(
                    Callable.From<float>(t =>
                    {
                        var arc = Vector2.Up * (Mathf.Sin(t * Mathf.Pi) * lift);
                        sprite.GlobalPosition = from.Lerp(to, t) + arc;
                        sprite.Scale = Vector2.One * scale * Mathf.Lerp(0.6f, 0.9f, t);
                        material.SetShaderParameter("opacity", Mathf.Clamp(t * 4f, 0f, 1f));
                    }),
                    0f,
                    1f,
                    Math.Max(duration, 0.01f))
                .SetEase(Tween.EaseType.InOut)
                .SetTrans(Tween.TransitionType.Sine);
            return StartMotion(tween);
        }

        /// <summary>Snaps onto the slot and bounces: the beat you see with the note you hear.</summary>
        internal Tween CreateLandingTween(Vector2 slot, float duration)
        {
            sprite.GlobalPosition = slot;
            material.SetShaderParameter("opacity", 1f);
            var tween = sprite.CreateTween();
            tween.TweenProperty(sprite, "scale", Vector2.One * scale, duration)
                .From(Vector2.One * scale * 1.28f)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Back);
            return StartMotion(tween);
        }

        /// <summary>Appears after <paramref name="delay"/>, fades while drifting, then frees itself.</summary>
        internal Tween CreateEchoTween(Vector2 from, Vector2 drift, float delay, float duration)
        {
            sprite.GlobalPosition = from;
            sprite.Scale = Vector2.One * scale;
            var tween = sprite.CreateTween();
            tween.TweenInterval(delay);
            tween.TweenMethod(
                    Callable.From<float>(t =>
                    {
                        sprite.GlobalPosition = from + drift * t;
                        material.SetShaderParameter("opacity", 0.5f * (1f - t));
                    }),
                    0f,
                    1f,
                    duration)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Quad);
            tween.TweenCallback(Callable.From(sprite.QueueFreeSafely));
            return StartMotion(tween);
        }

        internal void AddScatter(Tween parallel, Vector2 drift, float delay, float duration)
        {
            StopMotion();
            var start = sprite.GlobalPosition;
            parallel.TweenMethod(
                    Callable.From<float>(t =>
                    {
                        sprite.GlobalPosition = start + drift * t;
                        material.SetShaderParameter("opacity", 1f - t);
                    }),
                    0f,
                    1f,
                    duration)
                .SetDelay(delay)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Quad);
        }

        private Tween StartMotion(Tween tween)
        {
            StopMotion();
            _motion = tween;
            return tween;
        }

        private void StopMotion()
        {
            if (_motion is { } motion && GodotObject.IsInstanceValid(motion))
                motion.Kill();
            _motion = null;
        }
    }
}
