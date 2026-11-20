using SakuraMod.SakuraModCode.FourthAct.Fire;
using SakuraMod.SakuraModCode.FourthAct.Visuals;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode.FourthAct.Dark.Encounters;
using SakuraMod.SakuraModCode.FourthAct.Fire.Encounters;
using SakuraMod.SakuraModCode.FourthAct.Fire.Cards;
using MegaCrit.Sts2.Core.Entities.Cards;

public sealed class FireEnemyRulesSuite
{
    [Fact]
    public void FireCatalogKeepsTheAcceptedElitePoolAndEndpoint()
    {
        Assert.Equal([SourceCardIdentity.Sword, SourceCardIdentity.Libra], FireEnemyCatalog.EliteEncounters.Select(e => e.RewardIdentity));
        Assert.Equal(SourceCardIdentity.Firey, FireEnemyCatalog.BossEncounter.RewardIdentity);
        Assert.Equal(typeof(LightEncounter), FireEnemyCatalog.EndpointEncounterType);
    }

    [Theory]
    [InlineData(0, false, 12)]
    [InlineData(10, false, 42)]
    [InlineData(10, true, 52)]
    public void JudgmentUsesResolutionHandSize(int handSize, bool empowered, int expected) =>
        Assert.Equal(expected, FireEnemyRules.JudgmentDamage(handSize, empowered));

    [Fact]
    public void GlareIsANonEtherealExhaustStatusShownWithTheClearLayout()
    {
        var card = new Glare();
        Assert.Equal(CardType.Status, card.Type);
        Assert.Equal([CardKeyword.Exhaust], card.CanonicalKeywords);
        Assert.DoesNotContain(CardKeyword.Ethereal, card.CanonicalKeywords);
        Assert.Contains(typeof(Glare), SakuraContentRegistration.AllCardTypesForRegistration());
        Assert.Contains(typeof(Glare), SakuraContentRegistration.ClearLayoutOnlyCardTypes);
    }

    [Theory]
    [InlineData(312, 520, true)]
    [InlineData(313, 520, false)]
    [InlineData(327, 545, true)]
    [InlineData(328, 545, false)]
    [InlineData(600, 1001, true)]
    [InlineData(601, 1001, false)]
    public void LightEmpowermentIncludesSixtyPercentWithoutRoundingUp(int hp, int maxHp, bool expected) =>
        Assert.Equal(expected, FireEnemyRules.IsLightEmpowered(hp, maxHp));

    [Theory]
    [InlineData(5, 5, 4, 1, 9)]
    [InlineData(5, 5, -4, 9, 1)]
    [InlineData(1, 9, 4, 0, 10)]
    public void PendulumSwingClampsAtTheLethalEdges(int left, int right, int vote, int expectedLeft, int expectedRight)
    {
        var result = FireEnemyRules.Swing(left, right, vote);
        Assert.Equal((expectedLeft, expectedRight), result);
    }

    [Fact]
    public void BalanceMovesBothPansOneStepTowardCenter() =>
        Assert.Equal((4, 6), FireEnemyRules.Recenter(3, 7));

    [Fact]
    public void FacingVoteResolvesBeforeTheSeparateImbalancePoint()
    {
        var result = FireEnemyRules.ResolveLibraTurn(5, 5, 1, "RIGHT");

        Assert.Equal((4, 6), result.Vote);
        Assert.Equal((4, 7), result.Final);
        Assert.Equal(result.Vote, FireEnemyRules.ResolveLibraTurn(5, 5, 1, null).Final);
    }

    [Theory]
    [InlineData(0, 300f, 0.85f, 510f)]
    [InlineData(5, 300f, 0.85f, 630f)]
    [InlineData(10, 300f, 0.85f, 750f)]
    [InlineData(0, 706f, 0.62f, 510f)]
    [InlineData(5, 706f, 0.62f, 630f)]
    [InlineData(10, 706f, 0.62f, 711.14f)]
    public void LibraPanCentersUseTheSafeVisibleInterval(
        int points,
        float height,
        float scale,
        float expected)
    {
        var scaledHeight = height * scale;
        var center = LibraVisualLayout.PanCenterY(points, scaledHeight);

        Assert.InRange(center, expected - 0.01f, expected + 0.01f);
        Assert.True(center - scaledHeight * 0.5f >= LibraVisualLayout.SafeTop - 0.01f);
        Assert.True(center + scaledHeight * 0.5f <= LibraVisualLayout.SafeBottom + 0.01f);
    }
}
