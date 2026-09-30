using System.Text.Json;
using SakuraMod.SakuraModCode;
using SakuraMod.SakuraModCode.Cards;

public sealed class SongStaffVfxSuite
{
    [Fact]
    public void SongRouteDeclaresTheStaffSharedCelAndItsLineBurstFallback()
    {
        string[] expected =
        [
            SongStaffVfx.ScenePath,
            .. CelVfxSession.SharedAssetPaths,
            .. SakuraNativeHitFx.LineBurstAssetPaths
        ];
        foreach (var card in new MegaCrit.Sts2.Core.Models.CardModel[] { new ClowSong(), new SakuraSong() })
        {
            var declared = SakuraCardVfxAssets.RunAssetPaths(card).ToHashSet(StringComparer.Ordinal);
            RegressionTestHarness.Require(
                declared.SetEquals(expected),
                $"Expected {card.GetType().Name} to declare the staff scene, the shared cel assets, and the line burst it falls back to.");
        }
    }

    /// <summary>
    /// The song's rhythm: eight quarter beats, eight faster beats, then no waiting.
    /// Asserted on the real schedule so a retuned constant cannot quietly turn a
    /// big play into a cutscene.
    /// </summary>
    [Fact]
    public void BeatScheduleSpeedsUpThenStopsWaiting()
    {
        for (var i = 0; i < 40; i++)
        {
            var expected = i < 8 ? 0.30f : i < 16 ? 0.15f : 0f;
            RegressionTestHarness.Require(
                Math.Abs(SongBeatSchedule.GapBefore(i) - expected) < 1e-6f,
                $"Expected beat {i} to wait {expected}s after the previous beat.");
            RegressionTestHarness.Require(
                SongBeatSchedule.DrawsNote(i) == i < 16,
                $"Expected only the first sixteen beats to write a note (beat {i}).");
        }

        foreach (var beats in new[] { 1, 3, 8, 12, 16, 20, 60 })
        {
            RegressionTestHarness.Require(
                SongBeatSchedule.TotalWait(beats) <= 3.6f + 1e-4f,
                $"Expected a {beats}-beat song to wait at most 3.6s, got {SongBeatSchedule.TotalWait(beats):0.###}s.");
        }
    }

    /// <summary>
    /// The Voice card's Echo adds hits to the same song; each echo beat sits right
    /// behind its Voice beat, and the total never differs from the hits gameplay
    /// deals.
    /// </summary>
    [Fact]
    public void EchoBeatsFollowTheirVoiceCardWithoutChangingTheHitCount()
    {
        RegressionTestHarness.Require(
            SongBeatSchedule.EchoBeats([1, 3, 1]).SequenceEqual([false, false, true, true, false]),
            "Expected a Voice card with Echo 2 to sing its beat and then two echo beats, in exhaust order.");
        RegressionTestHarness.Require(
            SongBeatSchedule.EchoBeats([2], trailingPlainBeats: 2).SequenceEqual([false, true, false, false]),
            "Expected Clow's activated extra hits to close the song as plain beats.");
        RegressionTestHarness.Require(
            SongBeatSchedule.EchoBeats([]).Length == 0
            && SongBeatSchedule.EchoBeats([], trailingPlainBeats: 2).Length == 2,
            "Expected an empty selection to sing only the activated extra hits.");

        foreach (var hits in new[] { new[] { 1 }, new[] { 2, 1, 3 }, new[] { 1, 1, 1, 1 } })
        {
            RegressionTestHarness.Require(
                SongBeatSchedule.EchoBeats(hits, 2).Length == hits.Sum() + 2,
                "Expected the beat count to equal the hits gameplay deals.");
        }
    }

    [Fact]
    public void PitchRisesThroughTwoOctavesThenCyclesTheUpperOne()
    {
        var pitches = Enumerable.Range(0, 20).Select(SongBeatSchedule.PitchIndex).ToArray();
        RegressionTestHarness.Require(
            pitches.Take(10).SequenceEqual(Enumerable.Range(0, 10)),
            "Expected the first ten beats to climb the two-octave pentatonic scale in order.");
        RegressionTestHarness.Require(
            pitches.Skip(10).SequenceEqual([5, 6, 7, 8, 9, 5, 6, 7, 8, 9]),
            "Expected later beats to keep moving through the upper octave instead of holding one pitch.");

        var steps = Enumerable.Range(0, 20).Select(SongBeatSchedule.StaffStep).ToArray();
        RegressionTestHarness.Require(
            steps.All(static step => step is >= -1 and <= 9),
            "Expected every note to sit on the staff or the space just outside it, with no ledger lines.");
        RegressionTestHarness.Require(
            steps.Take(10).Zip(steps.Skip(1).Take(9)).All(static pair => pair.Second > pair.First),
            "Expected the written notes to rise with the pitch.");
    }

    [Fact]
    public void GlyphsFollowRhythmAndEngravingRules()
    {
        foreach (var beats in new[] { 1, 3, 8, 16 })
        {
            RegressionTestHarness.Require(
                SongBeatSchedule.GlyphFor(beats - 1, beats) == SongGlyph.Whole,
                $"Expected the closing beat of a {beats}-beat song to be a whole note.");
        }

        for (var i = 8; i < 15; i++)
        {
            RegressionTestHarness.Require(
                SongBeatSchedule.GlyphFor(i, 16) == SongGlyph.Eighth,
                $"Expected the fast run to be plain eighths (beat {i}).");
        }

        var slow = Enumerable.Range(0, 8).Select(static i => SongBeatSchedule.GlyphFor(i, 20)).Distinct().Count();
        RegressionTestHarness.Require(
            slow >= 4,
            "Expected the slow half to mix several note values rather than repeat one.");

        for (var i = 0; i < 16; i++)
        {
            RegressionTestHarness.Require(
                SongBeatSchedule.StemDown(i) == SongBeatSchedule.StaffStep(i) >= 4,
                $"Expected beat {i}'s stem to flip at the middle line.");
        }

        var cells = new HashSet<int>();
        foreach (var glyph in Enum.GetValues<SongGlyph>())
        {
            foreach (var down in new[] { false, true })
            {
                var cell = SongStaffVfx.CellFor(glyph, down);
                RegressionTestHarness.Require(
                    cell > SongStaffVfx.ClefCell && cell < SongStaffVfx.AtlasCellCount,
                    $"Expected {glyph} (down={down}) to map inside the note cells of the atlas.");
                cells.Add(cell);
            }
        }
        RegressionTestHarness.Require(
            cells.Count == SongStaffVfx.AtlasCellCount - 1,
            "Expected every note cell of the atlas to be used and no two glyphs to share one.");
    }

    /// <summary>
    /// The session hard-codes the atlas layout; the bake script writes it. A re-bake
    /// that moves the origin or reorders cells must fail here, not misalign notes
    /// in combat.
    /// </summary>
    [Fact]
    public void AtlasConstantsMatchTheBakedMetadata()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/images/card_vfx/song/glyphs_sdf.json")));
        var root = json.RootElement;
        var cell = root.GetProperty("cell_size_px");
        var origin = root.GetProperty("origin_px");
        var names = root.GetProperty("cells").EnumerateArray()
            .Select(static entry => entry.GetProperty("name").GetString())
            .ToArray();

        RegressionTestHarness.Require(
            Math.Abs(root.GetProperty("staff_space_px").GetSingle() - SongStaffVfx.AtlasStaffSpace) < 1e-4f
            && Math.Abs(cell[0].GetSingle() - SongStaffVfx.AtlasCell.X) < 1e-4f
            && Math.Abs(cell[1].GetSingle() - SongStaffVfx.AtlasCell.Y) < 1e-4f
            && Math.Abs(origin[0].GetSingle() - SongStaffVfx.AtlasOrigin.X) < 1e-4f
            && Math.Abs(origin[1].GetSingle() - SongStaffVfx.AtlasOrigin.Y) < 1e-4f,
            "Expected SongStaffVfx's atlas staff space, cell size, and origin to match glyphs_sdf.json.");
        RegressionTestHarness.Require(
            names.SequenceEqual(
            [
                "gClef",
                "noteQuarterUp", "noteQuarterDown",
                "note8thUp", "note8thDown",
                "noteHalfUp", "noteHalfDown",
                "noteQuarterUpDotted", "noteQuarterDownDotted",
                "noteWhole"
            ])
            && names.Length == SongStaffVfx.AtlasCellCount,
            "Expected the atlas cell order CellFor assumes.");
        RegressionTestHarness.Require(
            File.Exists(RegressionTestHarness.FindRepoFile("SakuraMod/images/card_vfx/song/OFL.txt")),
            "Expected the Bravura licence to ship beside the atlas.");
    }

    [Fact]
    public void EveryBeatAndChordHasASynthesizedNoteFile()
    {
        foreach (var timbre in Enum.GetValues<SongTimbre>())
        {
            var paths = Enumerable.Range(0, 20)
                .Select(i => SongStaffVfx.NotePath(timbre, SongBeatSchedule.PitchIndex(i)))
                .Append(SongStaffVfx.ChordPath(timbre))
                .Distinct();
            foreach (var path in paths)
            {
                var relative = path.Replace("res://", string.Empty, StringComparison.Ordinal);
                RegressionTestHarness.Require(
                    File.Exists(RegressionTestHarness.FindRepoFile(relative))
                    && File.Exists(RegressionTestHarness.FindRepoFile(relative + ".import")),
                    $"Expected {path} and its import to exist.");
            }
        }
    }

    [Fact]
    public void SongWaitsOnEachBeatAndFallsBackToTheLineBurst()
    {
        var song = File.ReadAllText(RegressionTestHarness.FindRepoFile("SakuraModCode/Cards/ClowSakura/Song.cs"));
        var vfx = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Visuals/Classic/SongStaffVfx.cs"));

        RegressionTestHarness.Require(
            song.Split("SongStaffVfx.PlayOrResolveAsync").Length - 1 == 2
            && song.Split("await cues.Beat(i, enemies);").Length - 1 == 2
            && song.Split("cues.Finale();").Length - 1 == 2,
            "Expected both Song cards to open the staff, wait on each beat, and close with the finale.");

        // The beat is awaited before that beat's damage, so the note, its pitch,
        // and the damage numbers land together.
        foreach (var block in song.Split("await cues.Beat(i, enemies);").Skip(1))
        {
            var damage = block.IndexOf("DealDamageToEnemies", StringComparison.Ordinal);
            var nextBeat = block.IndexOf("cues.Finale", StringComparison.Ordinal);
            RegressionTestHarness.Require(
                damage >= 0 && damage < nextBeat,
                "Expected each beat's damage to follow its awaited cue.");
        }

        RegressionTestHarness.Require(
            song.Contains("cues.IsLive ? null : SakuraNativeHitFx.LineBurst", StringComparison.Ordinal)
            && song.Split("hitVfxNode: ").Length - 1 == 2,
            "Expected Song to keep the vanilla line burst whenever the staff is not drawn.");
        RegressionTestHarness.Require(
            vfx.Contains("CelVfxSession.PlayOrResolveAsync", StringComparison.Ordinal)
            && vfx.Contains("PreloadManager.Cache.GetScene", StringComparison.Ordinal)
            && !vfx.Contains("ResourceLoader.Load", StringComparison.Ordinal)
            && vfx.Contains("SakuraGameVolumeFollower.VoiceFactor()", StringComparison.Ordinal),
            "Expected the staff to honour the card-VFX preference, read its scene from the run cache, and follow the game volume.");
    }

    [Fact]
    public void SongShadersTakeTheSessionClockAndTheSharedInk()
    {
        var staff = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/shaders/card_vfx/song_staff.gdshader"));
        var glyph = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/shaders/card_vfx/song_glyph.gdshader"));

        foreach (var (name, source) in new[] { ("song_staff", staff), ("song_glyph", glyph) })
        {
            RegressionTestHarness.Require(
                !source.Contains("TIME", StringComparison.Ordinal)
                && !source.Contains("hint_screen_texture", StringComparison.Ordinal),
                $"Expected {name} to take its clock from the session without shader TIME or screen sampling.");
        }

        RegressionTestHarness.Require(
            glyph.Contains("#include \"res://SakuraMod/shaders/card_vfx/cel_vfx.gdshaderinc\"", StringComparison.Ordinal)
            && glyph.Contains("CEL_INK_WIDTH", StringComparison.Ordinal),
            "Expected the glyph ink to use the shared ink width.");
        // The ribbon is the hybrid half of the effect: no dark ink.
        RegressionTestHarness.Require(
            !staff.Contains("cel_ink", StringComparison.Ordinal)
            && staff.Contains("uniform float elapsed", StringComparison.Ordinal),
            "Expected the staff ribbon to stay ink-free and driven by the session's elapsed clock.");
    }
}
