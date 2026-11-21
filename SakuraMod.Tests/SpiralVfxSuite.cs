using System.Text.RegularExpressions;
using SakuraMod.SakuraModCode;
using SakuraMod.SakuraModCode.Cards;

public sealed class SpiralVfxSuite
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void SpiralRoutesCorridorShaderAndSharedPreludeAssets()
    {
        var paths = SakuraCardVfxAssets.RunAssetPaths(new Spiral()).ToArray();

        RegressionTestHarness.Require(
            paths.Contains(SpiralCorridorVfx.ShaderPath),
            $"Expected Spiral run assets to include {SpiralCorridorVfx.ShaderPath}.");
        RegressionTestHarness.Require(
            CelVfxSession.SharedAssetPaths.All(paths.Contains),
            "Expected Spiral run assets to include shared CelVfxSession prelude assets.");
        RegressionTestHarness.Require(
            SakuraMagicCirclePresenter.AssetPaths.All(paths.Contains),
            "Expected Spiral run assets to include era magic-circle assets.");
        RegressionTestHarness.Require(
            paths.Length == paths.Distinct(StringComparer.Ordinal).Count(),
            "Expected Spiral run assets to be deduplicated.");
    }

    [Fact]
    public void SpiralShaderHonoursEngineeringContracts()
    {
        var shaderFile = Path.Combine(RepoRoot, "SakuraMod", "shaders", "card_vfx", "spiral_corridor.gdshader");
        RegressionTestHarness.Require(File.Exists(shaderFile), $"Missing shader file: {shaderFile}");

        var source = File.ReadAllText(shaderFile);
        var codeOnly = Regex.Replace(source, @"//[^\r\n]*", string.Empty);
        RegressionTestHarness.Require(
            !Regex.IsMatch(codeOnly, @"\bTIME\b"),
            "Spiral shader must not read engine TIME; the session owns every clock.");
        RegressionTestHarness.Require(
            !codeOnly.Contains("SCREEN_TEXTURE", StringComparison.Ordinal)
            && !codeOnly.Contains("hint_screen_texture", StringComparison.Ordinal)
            && !codeOnly.Contains("SCREEN_UV", StringComparison.Ordinal),
            "Spiral shader must not sample the screen.");

        var controllerFile = Path.Combine(
            RepoRoot, "SakuraModCode", "Cards", "Visuals", "Transparent", "SpiralCorridorVfx.cs");
        var controllerSource = File.ReadAllText(controllerFile);
        RegressionTestHarness.Require(
            !controllerSource.Contains("ResourceLoader.Load", StringComparison.Ordinal),
            "SpiralCorridorVfx must load its shader only through PreloadManager.Cache.");
    }

    [Fact]
    public void SpiralCardPlacesCuesAroundDamageAndNextTurnCopies()
    {
        var cardFile = Path.Combine(RepoRoot, "SakuraModCode", "Cards", "Transparent", "Spiral.cs");
        var source = File.ReadAllText(cardFile);

        var impactIdx = source.IndexOf("cues.Impact()", StringComparison.Ordinal);
        var damageIdx = source.IndexOf("DealDamage(", StringComparison.Ordinal);
        var fallbackIdx = source.IndexOf("hitVfx: cues.FallbackHitVfx", StringComparison.Ordinal);
        var scheduleIdx = source.IndexOf("ScheduleNextTurnCopies(", StringComparison.Ordinal);
        var returnIdx = source.IndexOf("cues.ReturnToDrawPile(", StringComparison.Ordinal);

        RegressionTestHarness.Require(
            impactIdx >= 0 && damageIdx > impactIdx && fallbackIdx > damageIdx,
            "Expected cues.Impact() immediately before DealDamage(..., hitVfx: cues.FallbackHitVfx).");
        RegressionTestHarness.Require(
            scheduleIdx > damageIdx && returnIdx > scheduleIdx,
            "Expected cues.ReturnToDrawPile(...) after ScheduleNextTurnCopies(...).");
    }

    [Fact]
    public void MemoryScalingAndInertCueFallbackRemainConsistent()
    {
        RegressionTestHarness.Require(
            SpiralCorridorVfx.MemoryTier(-2) == 0f
            && SpiralCorridorVfx.MemoryTier(0) == 0f
            && MathF.Abs(SpiralCorridorVfx.MemoryTier(4) - 0.5f) < 0.001f
            && SpiralCorridorVfx.MemoryTier(8) == 1f
            && SpiralCorridorVfx.MemoryTier(20) == 1f,
            "Expected Spiral memory tier to clamp cleanly across [0, 8].");

        RegressionTestHarness.Require(
            SpiralCorridorVfx.StepCountForMemory(0) == 14f
            && SpiralCorridorVfx.StepCountForMemory(8) == 22f
            && SpiralCorridorVfx.SpiralTurnsForMemory(8) > SpiralCorridorVfx.SpiralTurnsForMemory(0),
            "Expected Spiral step count and turns to grow monotonically with Memory count.");

        // Inert CueScope (e.g. VFX off, TestMode, or load failure) must preserve native Slash fallback
        // and execute gameplay exactly once without throwing.
        var gameplayCalls = 0;
        string? observedFallback = null;
        CelVfxSession.PlayOrResolveAsync<SpiralCorridorVfx>(
            presentationEnabled: false,
            label: "Spiral corridor",
            tryCreate: static () => throw new InvalidOperationException("Should not create when disabled."),
            playPrelude: static _ => Task.FromResult(false),
            resolveGameplay: scope =>
            {
                var cues = new SpiralCorridorVfx.Cues(scope);
                observedFallback = cues.FallbackHitVfx;
                cues.Impact();
                cues.ReturnToDrawPile(isExtra: true);
                gameplayCalls++;
                return Task.CompletedTask;
            },
            beginOutro: static _ => { },
            dispose: static _ => { }).GetAwaiter().GetResult();

        RegressionTestHarness.Require(
            gameplayCalls == 1 && observedFallback == SakuraNativeHitFx.Slash,
            "Expected inert Spiral cues to run gameplay once and fall back to native Slash.");
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            if (File.Exists(Path.Combine(dir, "SakuraMod.csproj")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new InvalidOperationException("Could not locate SakuraMod.csproj from test base directory.");
    }
}
