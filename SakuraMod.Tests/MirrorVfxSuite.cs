using Godot;
using SakuraMod.SakuraModCode;
using SakuraMod.SakuraModCode.Cards;

public sealed class MirrorVfxSuite
{
    private const string ShaderPath = "SakuraMod/shaders/card_vfx/mirror_arrival.gdshader";

    private static string Source(string path) =>
        File.ReadAllText(RegressionTestHarness.FindRepoFile(path));

    [Fact]
    public void ArrivalShaderIsPreloadedWithTheCharacter()
    {
        var character = Source("SakuraModCode/Character/ClassicSakura.cs");
        RegressionTestHarness.Require(
            character.Contains(".. SakuraMirrorArrivalOverlay.AssetPaths,", StringComparison.Ordinal)
            && File.Exists(RegressionTestHarness.FindRepoFile(ShaderPath))
            && !SakuraCardVfxAssets.RunAssetPaths(new Mirror()).Except(SakuraMagicCirclePresenter.AssetPaths).Any(),
            "Expected the arrival shader as a character run asset and no card-specific VFX route for Mirror.");
    }

    [Fact]
    public void ArrivalShaderNeverSamplesTheScreenOrTheEngineClock()
    {
        var shader = Source(ShaderPath);
        RegressionTestHarness.Require(
            !shader.Contains("SCREEN_", StringComparison.Ordinal)
            && !shader.Contains("TIME", StringComparison.Ordinal)
            && shader.Contains("uniform float progress", StringComparison.Ordinal),
            "Expected the arrival to be driven only by its tweened progress and never sample the overlapped hand.");
    }

    [Fact]
    public void ArrivalLayoutCentresOnEveryFaceWithRoomForTheReflection()
    {
        foreach (var layout in Enum.GetValues<SakuraCardVisualLayout>())
        {
            var (rootPosition, frame, cardSize) = SakuraMirrorArrivalPolicy.Layout(layout);
            var expectedRoot = layout == SakuraCardVisualLayout.None ? Vector2.Zero : cardSize * 0.5f;
            RegressionTestHarness.Require(
                rootPosition.IsEqualApprox(expectedRoot)
                && frame.GetCenter().IsEqualApprox(Vector2.Zero)
                && frame.Size.IsEqualApprox(cardSize + Vector2.One * SakuraMirrorArrivalPolicy.OuterMargin * 2f),
                $"Expected the arrival centred on the {layout} face with an even outer margin.");
        }
    }

    [Fact]
    public void MirrorPlaysTheArrivalAfterReplayWithoutWaitingOrPersistingState()
    {
        var card = Source("SakuraModCode/Cards/Transparent/Mirror.cs");
        var replay = card.IndexOf("card.BaseReplayCount += amount;", StringComparison.Ordinal);
        var arrival = card.IndexOf("SakuraMirrorArrivalOverlay.Play(card);", StringComparison.Ordinal);
        RegressionTestHarness.Require(
            replay >= 0 && replay < arrival
            && card.Contains("CardCmd.Preview(card);", StringComparison.Ordinal)
            && !card.Contains("await SakuraMirrorArrivalOverlay", StringComparison.Ordinal),
            "Expected Mirror to start the arrival right after granting Replay, keep the native preview, and add no wait.");

        var overlay = Source("SakuraModCode/Cards/SakuraMirrorArrivalOverlay.cs");
        RegressionTestHarness.Require(
            overlay.Contains("SakuraModConfig.IsCardVfxEnabled()", StringComparison.Ordinal)
            && overlay.Contains("PreloadManager.Cache.GetAsset<Shader>(ShaderPath)", StringComparison.Ordinal)
            && overlay.Contains("_tween.TweenCallback(Callable.From(Stop));", StringComparison.Ordinal)
            && !overlay.Contains("_cardOverlay", StringComparison.Ordinal)
            && !overlay.Contains("ResourceLoader.Load", StringComparison.Ordinal),
            "Expected the arrival to honour the card-VFX switch, read the preload cache, hide itself when done, and leave the native Affliction slot alone.");
    }

    [Fact]
    public void PooledCardNodesStopARunningArrival()
    {
        var lifecycle = Source("SakuraModCode/Cards/SakuraCardVisualLifecycle.cs");
        RegressionTestHarness.Require(
            lifecycle.Contains("SakuraMirrorArrivalOverlay.ResetForReuse(card);", StringComparison.Ordinal),
            "Expected pool reuse to stop a running Mirror arrival.");
    }
}
