using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.TestSupport;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.TestProtocol;
using SpiralEnchantment = MegaCrit.Sts2.Core.Models.Enchantments.Spiral;

namespace SakuraMod.RuntimeTests;

internal static class SpellTurnTransformationScenario
{
    public static async Task<Dictionary<string, object?>> ExecuteAsync(
        SakuraTestRequest request,
        RuntimeAssertionCollector assertions)
    {
        var context = await CombatScenarioContext.StartAsync(request);
        var combat = await context.EnterWeakCrawlerCombatAsync();
        var missedCacheAssetsBefore = InspectRunAssetCache(assertions);
        var player = context.Player;
        var playerCombat = player.PlayerCombatState
            ?? throw new InvalidOperationException("Player combat state is unavailable.");

        await VerifyTransientCardVfxAsync(combat.HittableEnemies.First(), assertions);

        var selectedClow = playerCombat.AllCards.OfType<ClowSword>().FirstOrDefault()
            ?? throw new InvalidOperationException("Starter combat did not contain ClowSword.");
        if (selectedClow.DeckVersion is not { } deckClow)
            throw new InvalidOperationException("Fixture ClowSword did not have a deck version.");
        CardCmd.Enchant(ModelDb.Enchantment<SpiralEnchantment>().ToMutable(), deckClow, 1m);
        if (selectedClow.Pile?.Type != PileType.Hand)
        {
            var moveAction = new RuntimeFixtureAction(
                player,
                _ => SakuraActions.MoveExistingCardToHand(null, selectedClow));
            await CombatScenarioContext.EnqueueAndWaitAsync(moveAction);
        }

        var deckClowBefore = player.Deck.Cards.OfType<ClowSword>().Count();
        var deckSakuraBefore = player.Deck.Cards.OfType<SakuraSword>().Count();
        var eligibleHandCards = playerCombat.Hand.Cards
            .Where(SakuraSourceCardRules.IsEligibleClowForTurn)
            .ToList();
        var selectedIndex = eligibleHandCards.IndexOf(selectedClow);
        if (selectedIndex < 0)
            throw new InvalidOperationException("Fixture ClowSword was not eligible for Turn.");

        var turn = await CombatScenarioContext.AddGeneratedCardToHandAsync<SpellTurn>(combat, player);
        assertions.True("turn_is_playable_before_conversion", turn.CanPlay());
        var selector = new TestCardSelector();
        selector.PrepareToSelect([selectedIndex]);
        using (CardSelectCmd.UseSelector(selector))
        {
            await CombatScenarioContext.PlayCardAsync(turn);
        }

        var handSakura = playerCombat.Hand.Cards.OfType<SakuraSword>().ToArray();
        var deckSakura = player.Deck.Cards.OfType<SakuraSword>().ToArray();
        assertions.Equal("deck_clow_decrement", deckClowBefore - 1, player.Deck.Cards.OfType<ClowSword>().Count());
        assertions.Equal("deck_sakura_increment", deckSakuraBefore + 1, deckSakura.Length);
        assertions.Equal("hand_sakura_count", 1, handSakura.Length);
        assertions.True(
            "deck_sakura_preserves_spiral",
            deckSakura.Single().Enchantment is SpiralEnchantment);
        assertions.True(
            "hand_sakura_preserves_spiral",
            handSakura.Single().Enchantment is SpiralEnchantment);
        assertions.Equal("selected_clow_removed_from_combat", false, playerCombat.AllCards.Contains(selectedClow));
        assertions.Equal("selected_clow_removed_from_hand", false, playerCombat.Hand.Cards.Contains(selectedClow));
        assertions.True("sakura_identity_registered", SakuraSourceCardRules.HasSakuraIdentity(player, selectedClow.Identity!.Value));
        assertions.True(
            "duplicate_sword_conversion_blocked",
            playerCombat.AllCards.OfType<ClowSword>().All(card => !SakuraSourceCardRules.IsEligibleClowForTurn(card)));
        assertions.Equal("turn_selector_released", null, CardSelectCmd.Selector);
        assertions.Equal(
            "card_vfx_playback_cache_misses",
            missedCacheAssetsBefore,
            PreloadManager.Cache.MissedCacheAssetCount);
        RuntimeTestHost.WriteCheckpoint(
            request,
            "spell_turn_transformation_verified",
            "SpellTurn converted a live ClowSword deck/hand identity through PlayCardAction.");

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["fixture"] = new
            {
                seed = request.Seed.ToString("X16"),
                selected_card = typeof(ClowSword).FullName,
                result_card = typeof(SakuraSword).FullName,
                setup_mutations = new[]
                {
                    "Moved one starter ClowSword combat card to hand when necessary",
                    "Generated SpellTurn into hand",
                    $"TestCardSelector index {selectedIndex} -> live ClowSword"
                }
            },
            ["after"] = new
            {
                deck_clow = player.Deck.Cards.OfType<ClowSword>().Count(),
                deck_sakura = player.Deck.Cards.OfType<SakuraSword>().Count(),
                hand_sakura = handSakura.Length,
                duplicate_sword_eligible = playerCombat.AllCards
                    .OfType<ClowSword>()
                    .Any(SakuraSourceCardRules.IsEligibleClowForTurn)
            }
        };
    }

    private static async Task VerifyTransientCardVfxAsync(
        Creature target, RuntimeAssertionCollector assertions)
    {
        var room = NCombatRoom.Instance ?? throw new InvalidOperationException("Card VFX needs a live room.");
        Node2D? Find(string name) => room.CombatVfxContainer.GetChildren().OfType<Node2D>()
            .SingleOrDefault(node => node.Name == name);
        ShaderMaterial? ice = null;
        var gameplayCalls = 0;
        await HailIceShardVfx.PlayOrResolveAsync(ModelDb.Card<Hail>(), null, [target], cues =>
        {
            gameplayCalls++;
            var root = Find("SakuraHailIceShardVfx")
                ?? throw new InvalidOperationException("Hail presentation failed to mount.");
            ice = (ShaderMaterial)root.GetNode<ColorRect>("Shards/HailIce1/ShardBody").Material;
            cues.Impact(target);
            assertions.Equal("hail_first_hit_cracks_immediately", 1f, ice.GetShaderParameter("crack").AsSingle());
            var grains = root.GetNode<Node2D>("Debris").GetChildCount();
            assertions.True("hail_first_hit_schedules_grains", grains > 0);
            cues.Impact(target);
            assertions.Equal("hail_duplicate_cue_does_not_duplicate_debris", grains,
                root.GetNode<Node2D>("Debris").GetChildCount());
            return Task.CompletedTask;
        });
        await CombatScenarioContext.WaitUntilAsync(
            () => ice is not null && ice.GetShaderParameter("split").AsSingle() > 0f,
            "Hail first-hit fracture movement");
        assertions.Equal("hail_one_hit_reaches_complete_fracture_path", 1f, ice!.GetShaderParameter("split").AsSingle());
        await CombatScenarioContext.WaitUntilAsync(() => Find("SakuraHailIceShardVfx") is null, "Hail cleanup");
        assertions.Equal("hail_gameplay_resolves_once", 1, gameplayCalls);

        foreach (var frozen in new[] { false, true })
        {
            var water = AquaWaterSphereVfx.TryCreate([target])
                ?? throw new InvalidOperationException("Aqua presentation failed to mount.");
            await water.PlayPrelude();
            var waterRoot = Find("SakuraAquaWaterSphereVfx")
                ?? throw new InvalidOperationException("Aqua ended before impact.");
            var body = waterRoot.GetNode<ColorRect>("Spheres/AquaWater1/WaterBody");
            var material = (ShaderMaterial)body.Material;
            assertions.Equal($"aqua_{frozen}_enclosure_completes_before_gameplay", 1f,
                material.GetShaderParameter("formation").AsSingle());
            water.Impact(target);
            if (frozen)
                water.PlayFreeze(target);
            water.Release();
            await CombatScenarioContext.WaitUntilAsync(() => Find("SakuraAquaWaterSphereVfx") is null,
                $"Aqua {(frozen ? "frozen" : "normal")} cleanup");
            assertions.True($"aqua_{frozen}_outro_cleans_up", Find("SakuraAquaWaterSphereVfx") is null);
        }

        SakuraCardPlayVfx.PlayTime();
        var clock = Find("SakuraTimeVfx") ?? throw new InvalidOperationException("Time presentation failed to mount.");
        assertions.Equal("time_is_centered_on_the_battlefield", room.SceneContainer.GetGlobalRect().GetCenter(), clock.GlobalPosition);
        assertions.True("time_native_animation_started", clock.GetNode<AnimationPlayer>("AnimationPlayer").IsPlaying());
        await CombatScenarioContext.WaitUntilAsync(() => Find("SakuraTimeVfx") is null, "Time animation cleanup");
        assertions.True("time_animation_releases_root", Find("SakuraTimeVfx") is null);
    }

    private static int InspectRunAssetCache(RuntimeAssertionCollector assertions)
    {
        CardModel[] vfxCards =
        [
            ModelDb.Card<Aqua>(),
            ModelDb.Card<Hail>(),
            ModelDb.Card<Blaze>(),
            ModelDb.Card<SakuraMod.SakuraModCode.Cards.Time>(),
            ModelDb.Card<ClowShield>(),
            ModelDb.Card<SakuraShield>(),
            ModelDb.Card<ClowSword>(),
            ModelDb.Card<SakuraSword>(),
            ModelDb.Card<Blade>(),
            ModelDb.Card<SpellTurn>()
        ];
        var expectedVfxPaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var card in vfxCards)
        {
            var runAssetPaths = card.RunAssetPaths.ToHashSet(StringComparer.Ordinal);
            foreach (var (path, index) in SakuraCardVfxAssets.RunAssetPaths(card)
                         .Distinct(StringComparer.Ordinal)
                         .Select(static (path, index) => (path, index)))
            {
                expectedVfxPaths.Add(path);
                assertions.True(
                    $"{card.Id.Entry}_vfx_run_asset_{index}",
                    runAssetPaths.Contains(path),
                    path);
            }
        }

        foreach (var (path, index) in expectedVfxPaths.Select(static (path, index) => (path, index)))
        {
            assertions.True(
                $"card_vfx_run_cache_{index}",
                PreloadManager.Cache.ContainsKey(path),
                path);
        }

        var missedCacheAssetsBefore = PreloadManager.Cache.MissedCacheAssetCount;
        var scenePaths = new[]
        {
            AquaWaterSphereVfx.ScenePath,
            AquaWaterSphereVfx.TargetScenePath,
            HailIceShardVfx.ScenePath,
            HailIceShardVfx.TargetScenePath,
            BlazeFireColumnVfx.ScenePath,
            SakuraCardPlayVfx.TimeScenePath,
            SakuraSwordBladeVfx.ScenePath,
            SakuraSwordBladeVfx.TargetScenePath,
            SpellTurnTransformationVfx.ScenePath
        };
        foreach (var (scenePath, index) in scenePaths.Select(static (path, index) => (path, index)))
        {
            var instance = PreloadManager.Cache.GetScene(scenePath).Instantiate();
            assertions.True(
                $"card_vfx_cached_scene_{index}",
                GodotObject.IsInstanceValid(instance),
                scenePath);
            instance.Dispose();
        }

        foreach (var audioPath in SpellTurnTransformationVfx.AssetPaths
                     .Where(static path => path.EndsWith(".ogg", StringComparison.Ordinal)))
        {
            assertions.True(
                $"spell_turn_cached_audio_{Path.GetFileNameWithoutExtension(audioPath)}",
                PreloadManager.Cache.GetAsset<AudioStream>(audioPath) is not null,
                audioPath);
        }

        assertions.Equal(
            "card_vfx_cache_inspection_misses",
            missedCacheAssetsBefore,
            PreloadManager.Cache.MissedCacheAssetCount);
        return missedCacheAssetsBefore;
    }
}
