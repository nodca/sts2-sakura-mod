using SakuraMod.SakuraModCode;
using SakuraMod.SakuraModCode.Cards;

public sealed class TransferVfxSuite
{
    private const string ShaderPath = "SakuraMod/shaders/card_vfx/transfer_lock.gdshader";

    [Fact]
    public void TransferRouteDeclaresTheLockShaderBesideTheMagicCircle()
    {
        var declared = SakuraCardVfxAssets.RunAssetPaths(new Transfer()).ToArray();
        RegressionTestHarness.Require(
            TransferVfx.AssetPaths.All(declared.Contains)
            && declared.Except(TransferVfx.AssetPaths).All(SakuraMagicCirclePresenter.AssetPaths.Contains)
            && declared.Length == declared.Distinct(StringComparer.Ordinal).Count(),
            "Expected Transfer to declare the lock shader plus only the shared magic circle.");
    }

    [Fact]
    public void LockShaderUsesTheSharedIncludeAndOwnsNoClock()
    {
        var shader = File.ReadAllText(RegressionTestHarness.FindRepoFile(ShaderPath));
        RegressionTestHarness.Require(
            shader.Contains("#include \"res://SakuraMod/shaders/card_vfx/cel_vfx.gdshaderinc\"", StringComparison.Ordinal)
            && !shader.Contains("TIME", StringComparison.Ordinal)
            && !shader.Contains("SCREEN_UV", StringComparison.Ordinal)
            && !shader.Contains("cel_smin(", StringComparison.Ordinal)
            && !shader.Contains("cel_fbm(", StringComparison.Ordinal),
            "Expected the Transfer shader to use the shared include, take time from the session, and keep its own lock operator.");
        // The card face: three ticks per reticle, an open outer side, and the
        // core travelling from the enemy to Sakura.
        RegressionTestHarness.Require(
            shader.Contains("uniform float outer_sign", StringComparison.Ordinal)
            && shader.Contains("mix(core_start, core_end", StringComparison.Ordinal),
            "Expected the reticle to open on its outer side and the core to run enemy -> Sakura.");
    }

    [Fact]
    public void ReticleRadiusStaysLegibleForEveryBody()
    {
        foreach (var body in new[] { new Godot.Vector2(60f, 90f), new Godot.Vector2(240f, 330f), new Godot.Vector2(500f, 600f) })
        {
            var radius = TransferVfx.ReticleRadius(body);
            RegressionTestHarness.Require(
                radius >= TransferVfx.MinReticleRadius && radius <= TransferVfx.MaxReticleRadius,
                "Expected the reticle radius clamped so ticks stay readable on tiny and huge bodies.");
        }
    }
}
