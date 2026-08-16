using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Content;
using SakuraMod.SakuraModCode.Cards;
using SakuraMod.SakuraModCode.Character;
using SakuraMod.SakuraModCode;
using System.Reflection;
using System.Text.Json;

internal static class RegressionTestHarness
{
    private const string PublicExportMarker = ".sakura_public_export";

    public static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    public static void RequireThrows<TException>(System.Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    public static void RequireFloatSequence(
        IReadOnlyList<float> actual,
        IReadOnlyList<float> expected,
        string message)
    {
        Require(actual.Count == expected.Count, message);
        for (var index = 0; index < actual.Count; index++)
        {
            Require(
                Math.Abs(actual[index] - expected[index]) < 0.001f,
                $"{message} Expected {expected[index]} at {index}, got {actual[index]}.");
        }
    }

    public static bool DeclaresMethod<T>(string methodName) =>
        typeof(T).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly) is not null;

    public static void RequireNoRemovedCardTypes(
        string owner,
        IEnumerable<Type> cardTypes,
        IReadOnlySet<string> removedTypeNames)
    {
        var present = cardTypes
            .Select(static type => type.Name)
            .Where(removedTypeNames.Contains)
            .Order()
            .ToList();

        Require(present.Count == 0, $"Expected {owner} not to include removed card type(s): {string.Join(", ", present)}.");
    }

    public static void RequireRegisteredCardLocalizationKeys(string relativePath, IEnumerable<Type> cardTypes)
    {
        var path = FindRepoFile(relativePath);
        var cards = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(path))
            ?? throw new InvalidOperationException($"Could not parse {relativePath}.");

        foreach (var type in cardTypes)
        {
            var entry = RegisteredModelEntry(type);
            Require(cards.ContainsKey($"{entry}.title"), $"Expected {relativePath} to localize {entry}.title.");
            Require(cards.ContainsKey($"{entry}.description"), $"Expected {relativePath} to localize {entry}.description.");

            if (!entry.StartsWith("SAKURA_MOD_CARD_", StringComparison.Ordinal))
                continue;

            var legacyPrefix = $"SAKURAMOD-{entry["SAKURA_MOD_CARD_".Length..]}.";
            Require(
                !cards.Keys.Any(key => key.StartsWith(legacyPrefix, StringComparison.Ordinal)),
                $"Expected {relativePath} to avoid legacy registered-card key {legacyPrefix}*.");
        }
    }

    public static void RequireRegisteredCharacterLocalizationKeys(string relativePath, IEnumerable<Type> characterTypes)
    {
        var path = FindRepoFile(relativePath);
        var characters = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(path))
            ?? throw new InvalidOperationException($"Could not parse {relativePath}.");

        foreach (var type in characterTypes)
        {
            var entry = RegisteredModelEntry(type);
            Require(characters.ContainsKey($"{entry}.title"), $"Expected {relativePath} to localize {entry}.title.");
            Require(characters.ContainsKey($"{entry}.description"), $"Expected {relativePath} to localize {entry}.description.");

            var legacyPrefix = $"SAKURAMOD-{RegisteredCharacterLegacyStem(type)}.";
            Require(
                !characters.Keys.Any(key => key.StartsWith(legacyPrefix, StringComparison.Ordinal)),
                $"Expected {relativePath} to avoid legacy registered-character key {legacyPrefix}*.");
        }
    }

    public static void RequireRegisteredPowerLocalizationKeys(string relativePath, IEnumerable<Type> powerTypes)
    {
        var path = FindRepoFile(relativePath);
        var powers = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(path))
            ?? throw new InvalidOperationException($"Could not parse {relativePath}.");

        foreach (var type in powerTypes)
        {
            var entry = RegisteredModelEntry(type);
            Require(powers.ContainsKey($"{entry}.title"), $"Expected {relativePath} to localize {entry}.title.");
            Require(powers.ContainsKey($"{entry}.description"), $"Expected {relativePath} to localize {entry}.description.");

            var legacyPrefix = $"SAKURAMOD-{ToUpperSnakeCase(type.Name)}.";
            Require(
                !powers.Keys.Any(key => key.StartsWith(legacyPrefix, StringComparison.Ordinal)),
                $"Expected {relativePath} to avoid legacy registered-power key {legacyPrefix}*.");
        }
    }

    public static void RequireRegisteredRelicLocalizationKeys(string relativePath, IEnumerable<Type> relicTypes)
    {
        var path = FindRepoFile(relativePath);
        var relics = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(path))
            ?? throw new InvalidOperationException($"Could not parse {relativePath}.");

        foreach (var type in relicTypes)
        {
            var entry = RegisteredModelEntry(type);
            Require(relics.ContainsKey($"{entry}.title"), $"Expected {relativePath} to localize {entry}.title.");
            Require(relics.ContainsKey($"{entry}.description"), $"Expected {relativePath} to localize {entry}.description.");
            Require(relics.ContainsKey($"{entry}.flavor"), $"Expected {relativePath} to localize {entry}.flavor.");

            var legacyPrefix = $"SAKURAMOD-{RegisteredRelicLegacyStem(type)}.";
            Require(
                !relics.Keys.Any(key => key.StartsWith(legacyPrefix, StringComparison.Ordinal)),
                $"Expected {relativePath} to avoid legacy registered-relic key {legacyPrefix}*.");
        }
    }

    public static void RequireNoLocalizationPrefixes(string relativePath, IEnumerable<string> prefixes)
    {
        var path = FindRepoFile(relativePath);
        var entries = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(path))
            ?? throw new InvalidOperationException($"Could not parse {relativePath}.");

        foreach (var prefix in prefixes)
        {
            Require(
                !entries.Keys.Any(key => key.StartsWith(prefix, StringComparison.Ordinal)),
                $"Expected {relativePath} not to contain removed localization key {prefix}*.");
        }
    }

    public static void RequireClassicFullFaceAssetsExist(IEnumerable<Type> cardTypes)
    {
        foreach (var type in cardTypes)
        {
            var card = Activator.CreateInstance(type) as SakuraSourceCard
                ?? throw new InvalidOperationException($"Expected {type.Name} to be a Classic Sakura card.");
            if (!SakuraCardVisualFamilies.UsesClassicLayout(card))
                continue;

            var familyDirectory = ClassicCardVisualAssets.FullFaceFamilyDirectory(card);
            var fileName = ClassicCardVisualAssets.ArtStem(type).NormalClassicArtStem();
            var relativePath = Path.Join("SakuraMod/images/cards/classic/full_faces", familyDirectory, fileName);
            var path = FindRepoFile(relativePath);
            Require(File.Exists($"{path}.import"), $"Expected {relativePath}.import to exist.");
        }
    }

    public static void RequireClassicEnergyCounterScene(string relativePath)
    {
        var scene = File.ReadAllText(FindRepoFile(relativePath));
        Require(
            scene.Contains("[node name=\"Layers\" type=\"Control\" parent=\".\"]", StringComparison.Ordinal)
            && scene.Contains("[node name=\"RotationLayers\" type=\"Control\" parent=\"Layers\"]", StringComparison.Ordinal)
            && scene.Contains("[node name=\"Label\" type=\"Label\" parent=\".\"]", StringComparison.Ordinal),
            "Expected the Classic Sakura energy counter to expose the native Layers, RotationLayers, and Label nodes.");
        var layer1Index = scene.IndexOf("[node name=\"Layer1\"", StringComparison.Ordinal);
        var layer2Index = scene.IndexOf("[node name=\"Layer2\"", StringComparison.Ordinal);
        var layer3Index = scene.IndexOf("[node name=\"Layer3\"", StringComparison.Ordinal);
        Require(
            layer1Index >= 0
            && layer1Index < layer2Index
            && layer2Index < layer3Index
            && System.Text.RegularExpressions.Regex.Matches(
                scene,
                "\\[node name=\\\"Layer[0-9]+\\\" type=\\\"TextureRect\\\"").Count == 3
            && scene.Contains("[node name=\"Layer1\" type=\"TextureRect\" parent=\"Layers\"]", StringComparison.Ordinal)
            && scene.Contains("[node name=\"Layer2\" type=\"TextureRect\" parent=\"Layers/RotationLayers\"]", StringComparison.Ordinal)
            && scene.Contains("[node name=\"Layer3\" type=\"TextureRect\" parent=\"Layers/RotationLayers\"]", StringComparison.Ordinal),
            "Expected exactly three ordered texture layers in the Classic Sakura energy counter.");
        Require(
            scene.Contains("sakura_combat_energy_counter_badge.png", StringComparison.Ordinal)
            && scene.Contains("empty_energy_counter_layer.png", StringComparison.Ordinal),
            "Expected the Classic Sakura energy counter to use the badge and transparent rotating-layer resources.");
    }

    public static string FindRepoFile(string relativePath) =>
        FindRepoPath(relativePath, File.Exists, static path =>
            new FileNotFoundException($"Could not find {path} from {AppContext.BaseDirectory}."));

    public static string FindRepoDirectory(string relativePath) =>
        FindRepoPath(relativePath, Directory.Exists, static path =>
            new DirectoryNotFoundException($"Could not find {path} from {AppContext.BaseDirectory}."));

    private static string FindRepoPath(
        string relativePath,
        Func<string, bool> exists,
        Func<string, Exception> createMissingException)
    {
        string? repositoryRoot = null;
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SakuraMod.csproj")))
                repositoryRoot ??= directory.FullName;

            var path = Path.Combine(directory.FullName, relativePath);
            if (exists(path))
                return path;
        }

        if (repositoryRoot is not null
            && File.Exists(Path.Combine(repositoryRoot, PublicExportMarker))
            && IsIntentionallyOmittedFromPublicExport(relativePath))
        {
            throw Xunit.Sdk.SkipException.ForSkip(
                $"Public source export intentionally omits '{relativePath}'. " +
                "This test requires private media, research, or service inputs.");
        }

        throw createMissingException(relativePath);
    }

    private static bool IsIntentionallyOmittedFromPublicExport(string relativePath) =>
        relativePath.StartsWith("SakuraMod/images/", StringComparison.Ordinal)
        || relativePath.StartsWith("SakuraMod/shaders/", StringComparison.Ordinal)
        || relativePath.StartsWith("SakuraMod/music/", StringComparison.Ordinal)
        || relativePath.StartsWith("SakuraMod/sfx/", StringComparison.Ordinal)
        || relativePath.StartsWith("SakuraMod/voices/", StringComparison.Ordinal)
        || relativePath.Equals("SakuraMod/mod_image.png", StringComparison.Ordinal)
        || relativePath.StartsWith("research/", StringComparison.Ordinal)
        || relativePath.StartsWith("tools/", StringComparison.Ordinal);

    public static T MutableForCostTest<T>(T card) where T : CardModel
    {
        typeof(AbstractModel)
            .GetMethod("NeverEverCallThisOutsideOfTests_SetIsMutable", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(card, [true]);
        return card;
    }

    public static string RegisteredModelEntry(Type type) =>
        ModContentRegistry.GetFixedPublicEntry(MainFile.ModId, type);

    private static string RegisteredCharacterLegacyStem(Type type) =>
        type == typeof(ClassicSakura) ? "CLASSIC_SAKURA" : ToUpperSnakeCase(type.Name);

    private static string RegisteredRelicLegacyStem(Type type)
    {
        const string relicSuffix = "Relic";
        var name = type.Name.EndsWith(relicSuffix, StringComparison.Ordinal)
            ? type.Name[..^relicSuffix.Length]
            : type.Name;

        return $"{ToUpperSnakeCase(name)}_RELIC";
    }

    private static string ToUpperSnakeCase(string value)
    {
        var builder = new System.Text.StringBuilder(value.Length + 8);
        for (var i = 0; i < value.Length; i++)
        {
            var current = value[i];
            if (i > 0
                && char.IsUpper(current)
                && (char.IsLower(value[i - 1])
                    || char.IsDigit(value[i - 1])
                    || (i + 1 < value.Length && char.IsLower(value[i + 1]))))
                builder.Append('_');

            builder.Append(char.ToUpperInvariant(current));
        }

        return builder.ToString();
    }
}
