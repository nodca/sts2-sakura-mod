using SakuraMod.SakuraModCode.Cards;

public sealed class CelVfxOrchestrationSuite
{
    [Fact]
    public void FollowThroughIsZeroAtRestAndBoundedUnderMotion()
    {
        var lag = new CelVfxFollowThrough();
        for (var i = 0; i < 120; i++)
            lag.Update(1f / 60f, 0.4f, 1f, 1f);
        Assert.Equal(0f, lag.Sway);
        Assert.Equal(0f, lag.Flex);

        // A violent turn and wing fling saturate but never exceed the shader range.
        var rotation = 0f;
        var spread = 0f;
        for (var i = 0; i < 30; i++)
        {
            rotation += 0.5f;
            spread = Math.Min(spread + 0.2f, 1f);
            lag.Update(1f / 60f, rotation, spread, 1f);
            Assert.InRange(lag.Sway, -1f, 1f);
            Assert.InRange(lag.Flex, -1f, 1f);
        }
    }

    [Fact]
    public void FollowThroughTrailsTheTurnThenSettles()
    {
        var lag = new CelVfxFollowThrough();
        lag.Update(1f / 60f, 0f, 0f, 1f);
        var rotation = 0f;
        for (var i = 0; i < 6; i++)
        {
            rotation += 0.08f;
            lag.Update(1f / 60f, rotation, 0f, 1f);
        }
        // Turning clockwise: the plumage lags the other way.
        Assert.True(lag.Sway < -0.2f, $"Expected a trailing lag, got {lag.Sway}.");

        // A mirrored creature turning the same way on screen lags the other way in its own frame.
        var mirrored = new CelVfxFollowThrough();
        mirrored.Update(1f / 60f, 0f, 0f, -1f);
        rotation = 0f;
        for (var i = 0; i < 6; i++)
        {
            rotation += 0.08f;
            mirrored.Update(1f / 60f, rotation, 0f, -1f);
        }
        Assert.True(mirrored.Sway > 0.2f, $"Expected the mirrored lag to flip, got {mirrored.Sway}.");

        for (var i = 0; i < 30; i++)
            lag.Update(1f / 60f, rotation, 0f, 1f);
        Assert.InRange(lag.Sway, -0.02f, 0.02f);
    }

    [Fact]
    public void FollowThroughSurvivesAFrameHitch()
    {
        var lag = new CelVfxFollowThrough();
        lag.Update(1f / 60f, 0f, 0f, 1f);
        lag.Update(0.2f, 0.3f, 0.5f, 1f);
        Assert.True(float.IsFinite(lag.Sway) && float.IsFinite(lag.Flex));
        Assert.InRange(lag.Sway, -1f, 1f);
        Assert.InRange(lag.Flex, -1f, 1f);
        for (var i = 0; i < 60; i++)
            lag.Update(1f / 60f, 0.3f, 0.5f, 1f);
        Assert.InRange(lag.Sway, -0.02f, 0.02f);
        Assert.InRange(lag.Flex, -0.02f, 0.02f);
    }

    [Fact]
    public async Task SuccessfulPlaybackPreservesPreludeCueGameplayAndOutroOrder()
    {
        var events = new List<string>();
        var resolved = 0;
        var playback = new FakePlayback(events);

        await CelVfxSession.PlayOrResolveAsync(
            true,
            "test",
            () =>
            {
                events.Add("create");
                return playback;
            },
            session => session.PlayPrelude(),
            async cues =>
            {
                resolved++;
                cues.Invoke("impact", session => session.Impact());
                events.Add("gameplay");
                await Task.CompletedTask;
            },
            session => session.BeginOutro(),
            session => session.Dispose(),
            (_, _) => events.Add("log"));

        Assert.Equal(1, resolved);
        Assert.Equal(["create", "prelude", "impact", "gameplay", "outro"], events);
        Assert.Equal(0, playback.DisposeCount);
    }

    [Fact]
    public async Task MissingOrFailedPresentationStillResolvesGameplayOnce()
    {
        var missingResolved = 0;
        await CelVfxSession.PlayOrResolveAsync<FakePlayback>(
            true,
            "missing",
            () => null,
            session => session.PlayPrelude(),
            cues =>
            {
                missingResolved++;
                cues.Invoke("impact", session => session.Impact());
                return Task.CompletedTask;
            },
            session => session.BeginOutro(),
            session => session.Dispose());

        var events = new List<string>();
        var failedResolved = 0;
        var failedPlayback = new FakePlayback(events) { PreludeResult = false };
        await CelVfxSession.PlayOrResolveAsync(
            true,
            "failed prelude",
            () => failedPlayback,
            session => session.PlayPrelude(),
            cues =>
            {
                failedResolved++;
                cues.Invoke("impact", session => session.Impact());
                events.Add("gameplay");
                return Task.CompletedTask;
            },
            session => session.BeginOutro(),
            session => session.Dispose());

        Assert.Equal(1, missingResolved);
        Assert.Equal(1, failedResolved);
        Assert.Equal(["prelude", "dispose", "gameplay"], events);
        Assert.Equal(1, failedPlayback.DisposeCount);
    }

    [Fact]
    public async Task FactoryAndPreludeExceptionsFailOpenWithoutRepeatingGameplay()
    {
        var factoryFailures = new List<string>();
        var factoryResolved = 0;
        await CelVfxSession.PlayOrResolveAsync<FakePlayback>(
            true,
            "factory",
            () => throw new InvalidOperationException("factory failed"),
            session => session.PlayPrelude(),
            _ =>
            {
                factoryResolved++;
                return Task.CompletedTask;
            },
            session => session.BeginOutro(),
            session => session.Dispose(),
            (stage, _) => factoryFailures.Add(stage));

        var events = new List<string>();
        var preludeFailures = new List<string>();
        var preludeResolved = 0;
        var playback = new FakePlayback(events) { PreludeFailure = new InvalidOperationException("prelude failed") };
        await CelVfxSession.PlayOrResolveAsync(
            true,
            "prelude",
            () => playback,
            session => session.PlayPrelude(),
            _ =>
            {
                preludeResolved++;
                events.Add("gameplay");
                return Task.CompletedTask;
            },
            session => session.BeginOutro(),
            session => session.Dispose(),
            (stage, _) => preludeFailures.Add(stage));

        Assert.Equal(1, factoryResolved);
        Assert.Equal(["create"], factoryFailures);
        Assert.Equal(1, preludeResolved);
        Assert.Equal(["prelude", "dispose", "gameplay"], events);
        Assert.Equal(["prelude"], preludeFailures);
        Assert.Equal(1, playback.DisposeCount);
    }

    [Fact]
    public async Task FirstCueFailureMakesLaterCuesInertAndCleansUpOnce()
    {
        var events = new List<string>();
        var failures = new List<string>();
        var playback = new FakePlayback(events) { ImpactFailure = new InvalidOperationException("impact failed") };

        await CelVfxSession.PlayOrResolveAsync(
            true,
            "test",
            () => playback,
            session => session.PlayPrelude(),
            cues =>
            {
                cues.Invoke("first impact", session => session.Impact());
                cues.Invoke("second impact", session => session.Impact());
                events.Add("gameplay");
                return Task.CompletedTask;
            },
            session => session.BeginOutro(),
            session => session.Dispose(),
            (stage, _) => failures.Add(stage));

        Assert.Equal(["prelude", "impact", "dispose", "gameplay"], events);
        Assert.Equal(["first impact"], failures);
        Assert.Equal(1, playback.DisposeCount);
    }

    [Fact]
    public async Task GameplayFailureIsRethrownAfterCleanupEvenWhenCleanupFails()
    {
        var gameplayFailure = new ApplicationException("gameplay failed");
        var playback = new FakePlayback([]) { DisposeFailure = new InvalidOperationException("cleanup failed") };

        var thrown = await Assert.ThrowsAsync<ApplicationException>(() =>
            CelVfxSession.PlayOrResolveAsync(
                true,
                "test",
                () => playback,
                session => session.PlayPrelude(),
                _ => Task.FromException(gameplayFailure),
                session => session.BeginOutro(),
                session => session.Dispose(),
                (_, _) => throw new InvalidOperationException("reporting failed")));

        Assert.Same(gameplayFailure, thrown);
        Assert.Equal(1, playback.DisposeCount);
    }

    [Fact]
    public async Task CleanupFailureIsReportedWithoutEscapingOrRepeatingGameplay()
    {
        var events = new List<string>();
        var failures = new List<string>();
        var resolved = 0;
        var playback = new FakePlayback(events)
        {
            PreludeResult = false,
            DisposeFailure = new InvalidOperationException("cleanup failed")
        };

        await CelVfxSession.PlayOrResolveAsync(
            true,
            "cleanup",
            () => playback,
            session => session.PlayPrelude(),
            _ =>
            {
                resolved++;
                events.Add("gameplay");
                return Task.CompletedTask;
            },
            session => session.BeginOutro(),
            session => session.Dispose(),
            (stage, _) => failures.Add(stage));

        Assert.Equal(1, resolved);
        Assert.Equal(["prelude", "dispose", "gameplay"], events);
        Assert.Equal(["cleanup"], failures);
        Assert.Equal(1, playback.DisposeCount);
    }

    [Fact]
    public async Task OutroFailureIsLoggedAndHardDisposedWithoutEscaping()
    {
        var events = new List<string>();
        var failures = new List<string>();
        var playback = new FakePlayback(events) { OutroFailure = new InvalidOperationException("outro failed") };

        await CelVfxSession.PlayOrResolveAsync(
            true,
            "outro",
            () => playback,
            session => session.PlayPrelude(),
            _ =>
            {
                events.Add("gameplay");
                return Task.CompletedTask;
            },
            session => session.BeginOutro(),
            session => session.Dispose(),
            (stage, _) => failures.Add(stage));

        Assert.Equal(["prelude", "gameplay", "outro", "dispose"], events);
        Assert.Equal(["outro"], failures);
        Assert.Equal(1, playback.DisposeCount);
    }

    [Fact]
    public async Task DisabledPresentationSkipsFactoryAndResolvesGameplayOnce()
    {
        var factoryCalls = 0;
        var gameplayCalls = 0;

        await CelVfxSession.PlayOrResolveAsync<FakePlayback>(
            false,
            "disabled",
            () =>
            {
                factoryCalls++;
                return new FakePlayback([]);
            },
            session => session.PlayPrelude(),
            _ =>
            {
                gameplayCalls++;
                return Task.CompletedTask;
            },
            session => session.BeginOutro(),
            session => session.Dispose());

        Assert.Equal(0, factoryCalls);
        Assert.Equal(1, gameplayCalls);
    }

    private sealed class FakePlayback(List<string> events)
    {
        internal bool PreludeResult { get; init; } = true;
        internal Exception? PreludeFailure { get; init; }
        internal Exception? ImpactFailure { get; init; }
        internal Exception? OutroFailure { get; init; }
        internal Exception? DisposeFailure { get; init; }
        internal int DisposeCount { get; private set; }

        internal Task<bool> PlayPrelude()
        {
            events.Add("prelude");
            if (PreludeFailure is not null)
                throw PreludeFailure;
            return Task.FromResult(PreludeResult);
        }

        internal void Impact()
        {
            events.Add("impact");
            if (ImpactFailure is not null)
                throw ImpactFailure;
        }

        internal void BeginOutro()
        {
            events.Add("outro");
            if (OutroFailure is not null)
                throw OutroFailure;
        }

        internal void Dispose()
        {
            DisposeCount++;
            events.Add("dispose");
            if (DisposeFailure is not null)
                throw DisposeFailure;
        }
    }
}
