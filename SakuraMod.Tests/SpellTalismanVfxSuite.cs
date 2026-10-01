using MegaCrit.Sts2.Core.Models;
using SakuraMod.SakuraModCode.Cards;

public sealed class SpellTalismanVfxSuite
{
    private static readonly (CardModel Card, string File)[] Spells =
    [
        (new SpellHuoShen(), "SpellHuoShen.cs"),
        (new SpellLeiDi(), "SpellLeiDi.cs"),
        (new SpellShuiLong(), "SpellShuiLong.cs"),
        (new SpellFengHua(), "SpellFengHua.cs")
    ];

    [Fact]
    public void EachElementSpellDeclaresTheSceneAndItsOwnGlyphMask()
    {
        var masks = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (card, _) in Spells)
        {
            var glyph = SpellTalismanVfx.GlyphFor(card);
            RegressionTestHarness.Require(glyph is not null, $"Expected {card.GetType().Name} to have a talisman glyph.");

            var mask = SpellTalismanVfx.GlyphMaskPath(glyph!.Value.Stem);
            var declared = SakuraCardVfxAssets.RunAssetPaths(card).ToArray();
            RegressionTestHarness.Require(
                declared.SequenceEqual([SpellTalismanVfx.ScenePath, mask]),
                $"Expected {card.GetType().Name} to declare exactly the talisman scene and its glyph mask.");
            masks.Add(mask);

            foreach (var path in declared)
            {
                var relative = path.Replace("res://", string.Empty, StringComparison.Ordinal);
                RegressionTestHarness.Require(
                    File.Exists(RegressionTestHarness.FindRepoFile(relative)),
                    $"Expected {path} to exist.");
            }
            RegressionTestHarness.Require(
                File.Exists(RegressionTestHarness.FindRepoFile(
                    mask.Replace("res://", string.Empty, StringComparison.Ordinal) + ".import")),
                $"Expected {mask} to ship with its import.");
        }

        RegressionTestHarness.Require(masks.Count == Spells.Length, "Expected every element spell to use its own mask.");
        RegressionTestHarness.Require(
            SpellTalismanVfx.GlyphFor(new SpellTurn()) is null
            && !SakuraCardVfxAssets.RunAssetPaths(new SpellTurn()).Contains(SpellTalismanVfx.ScenePath),
            "Expected the talisman to stay limited to the four element spells.");
    }

    /// <summary>The talisman may hold combat up for at most 0.6s before damage.</summary>
    [Fact]
    public void ReleaseWaitsForTheCatchAndOneSteppedFrameWithinBudget()
    {
        RegressionTestHarness.Require(
            SpellTalismanVfx.ReleaseAt > SpellTalismanVfx.CatchDuration
            && SpellTalismanVfx.ReleaseAt <= 0.6f,
            $"Expected damage to wait for the lit glyph but no longer than 0.6s, got {SpellTalismanVfx.ReleaseAt:0.###}s.");
    }

    [Fact]
    public async Task DisabledPresentationReleasesAtOnceAndResolvesGameplayOnce()
    {
        var factoryCalls = 0;
        var gameplayCalls = 0;
        var releasedAtOnce = false;

        await CelVfxSession.PlayOrResolveAsync<SpellTalismanVfx>(
            false,
            "spell talisman",
            () =>
            {
                factoryCalls++;
                return null;
            },
            _ => Task.FromResult(true),
            scope =>
            {
                gameplayCalls++;
                releasedAtOnce = new SpellTalismanVfx.Cues(scope).Release().IsCompletedSuccessfully;
                return Task.CompletedTask;
            },
            _ => { },
            _ => { });

        Assert.Equal(0, factoryCalls);
        Assert.Equal(1, gameplayCalls);
        Assert.True(releasedAtOnce);
    }

    [Fact]
    public void EachSpellReleasesTheTalismanBeforeItsDamage()
    {
        foreach (var (card, file) in Spells)
        {
            var source = File.ReadAllText(RegressionTestHarness.FindRepoFile("SakuraModCode/Cards/Spells/" + file));
            var open = source.IndexOf("SpellTalismanVfx.PlayOrResolveAsync(this", StringComparison.Ordinal);
            var release = source.IndexOf("await cues.Release();", StringComparison.Ordinal);
            var damage = source.IndexOf("await DealDamage", StringComparison.Ordinal);
            RegressionTestHarness.Require(
                open >= 0 && open < release && release < damage
                && source.Split("await DealDamage").Length == 2,
                $"Expected {card.GetType().Name} to open the talisman, release it, and then deal its damage once.");
        }

        var leiDi = File.ReadAllText(RegressionTestHarness.FindRepoFile("SakuraModCode/Cards/Spells/SpellLeiDi.cs"));
        RegressionTestHarness.Require(
            leiDi.IndexOf("CombatCardSelection.NextItem", StringComparison.Ordinal)
                < leiDi.IndexOf("if (target is null)", StringComparison.Ordinal)
            && leiDi.IndexOf("if (target is null)", StringComparison.Ordinal)
                < leiDi.IndexOf("SpellTalismanVfx.PlayOrResolveAsync", StringComparison.Ordinal),
            "Expected Lei Di to pick its target first and skip the talisman when nothing can be hit.");
    }

    [Fact]
    public void SessionFollowsTheRenderersFaceAndReadsOnlyTheRunCache()
    {
        var vfx = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Visuals/Classic/SpellTalismanVfx.cs"));
        var renderer = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraModCode/Cards/Visuals/Classic/ClassicSakuraVisualPatch.cs"));

        RegressionTestHarness.Require(
            renderer.Contains($"Name = \"{SpellTalismanVfx.FaceNodeName}\"", StringComparison.Ordinal),
            "Expected the full-face renderer to still name its face node the way the talisman looks it up.");
        RegressionTestHarness.Require(
            vfx.Contains("CelVfxSession.PlayOrResolveAsync", StringComparison.Ordinal)
            && vfx.Contains("PreloadManager.Cache.GetScene", StringComparison.Ordinal)
            && vfx.Contains("PreloadManager.Cache.GetAsset<Texture2D>", StringComparison.Ordinal)
            && !vfx.Contains("ResourceLoader.Load", StringComparison.Ordinal),
            "Expected the talisman to honour the card-VFX preference and read its resources from the run cache.");
    }

    [Fact]
    public void ShaderTakesTheSessionClockAndTheSharedInclude()
    {
        var shader = File.ReadAllText(RegressionTestHarness.FindRepoFile(
            "SakuraMod/shaders/card_vfx/spell_talisman.gdshader"));

        RegressionTestHarness.Require(
            !shader.Contains("TIME", StringComparison.Ordinal)
            && !shader.Contains("hint_screen_texture", StringComparison.Ordinal)
            && shader.Contains("#include \"res://SakuraMod/shaders/card_vfx/cel_vfx.gdshaderinc\"", StringComparison.Ordinal)
            && shader.Contains("uniform float elapsed", StringComparison.Ordinal)
            && shader.Contains("uniform float opacity", StringComparison.Ordinal),
            "Expected the talisman shader to take its clock and fade from the session, not shader TIME.");
    }
}
