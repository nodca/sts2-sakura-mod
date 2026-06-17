using Godot;

namespace SakuraMod.SakuraModCode.Extensions;

//Mostly utilities to get asset paths.
public static class StringExtensions
{
    private const string ImportPathPrefix = "path=\"";

    // Resolving a texture path opens and line-reads the asset's ".import" file. The card frame
    // applier and the card/power/relic icon getters hit these helpers many times per card view
    // (e.g. Kindness builds a 30-card discover grid, ~5 path lookups per card), so memoize each
    // candidate path's resolved result. Exported asset paths are fixed at runtime, so the cache
    // never needs invalidation. UI runs on the main thread, matching the other texture caches.
    private static readonly Dictionary<string, string> ResolvedPathCache = [];

    public static string ImagePath(this string path)
    {
        return Path.Join(MainFile.ResPath, "images", path);
    }

    public static string CardImagePath(this string path) =>
        ResolveCached(
            Path.Join(MainFile.ResPath, "images", "card_portraits", path),
            "Could not find card image path: ",
            static () => Path.Join(MainFile.ResPath, "images", "card_portraits", "card.png"));

    public static string BigCardImagePath(this string path) =>
        ResolveCached(
            Path.Join(MainFile.ResPath, "images", "card_portraits", "big", path),
            "Could not find big card image path: ",
            static () => Path.Join(MainFile.ResPath, "images", "card_portraits", "big", "card.png"));

    public static string ClearCardImagePath(this string path) =>
        ResolveCached(
            path.ClearCardAssetPath(),
            "Could not find clear card image path: ",
            static () => Path.Join(MainFile.ResPath, "images", "card_portraits", "big", "card.png"));

    public static string ClearCardAssetPath(this string path)
    {
        return Path.Join(MainFile.ResPath, "images", "cards", "clear_cards", path);
    }

    public static string PowerImagePath(this string path) =>
        ResolveCached(
            Path.Join(MainFile.ResPath, "images", "powers", path),
            "Could not find power image path: ",
            static () => Path.Join(MainFile.ResPath, "images", "powers", "power.png"));

    public static string BigPowerImagePath(this string path) =>
        ResolveCached(
            Path.Join(MainFile.ResPath, "images", "powers", "big", path),
            "Could not find big power image path: ",
            static () => Path.Join(MainFile.ResPath, "images", "powers", "big", "power.png"));

    public static string RelicImagePath(this string path) =>
        ResolveCached(
            Path.Join(MainFile.ResPath, "images", "relics", path),
            "Could not find relic image path: ",
            static () => Path.Join(MainFile.ResPath, "images", "relics", "relic.png"));

    public static string BigRelicImagePath(this string path) =>
        ResolveCached(
            Path.Join(MainFile.ResPath, "images", "relics", "big", path),
            "Could not find big relic image path: ",
            static () => Path.Join(MainFile.ResPath, "images", "relics", "big", "relic.png"));

    public static string CharacterUiPath(this string path)
    {
        var candidate = Path.Join(MainFile.ResPath, "images", "charui", path);
        if (ResolvedPathCache.TryGetValue(candidate, out var cached))
            return cached;

        var resolved = TryResolveTexturePath(candidate, out var resolvedPath) ? resolvedPath : candidate;
        ResolvedPathCache[candidate] = resolved;
        return resolved;
    }

    private static string ResolveCached(string candidatePath, string missingLogPrefix, Func<string> defaultPathFactory)
    {
        if (ResolvedPathCache.TryGetValue(candidatePath, out var cached))
            return cached;

        string resolved;
        if (TryResolveTexturePath(candidatePath, out var resolvedPath))
        {
            resolved = resolvedPath;
        }
        else
        {
            MainFile.Logger.Info(missingLogPrefix + candidatePath);
            resolved = ResolveDefaultTexturePath(defaultPathFactory());
        }

        ResolvedPathCache[candidatePath] = resolved;
        return resolved;
    }

    private static string ResolveDefaultTexturePath(string path)
    {
        return TryResolveTexturePath(path, out var resolvedPath) ? resolvedPath : path;
    }

    private static bool TryResolveTexturePath(string path, out string resolvedPath)
    {
        if (TryGetImportedTexturePath(path, out resolvedPath) && ResourceLoader.Exists(resolvedPath))
            return true;

        resolvedPath = path;
        return ResourceLoader.Exists(path);
    }

    private static bool TryGetImportedTexturePath(string path, out string importedPath)
    {
        importedPath = string.Empty;
        var importPath = path + ".import";

        if (!Godot.FileAccess.FileExists(importPath))
            return false;

        using var file = Godot.FileAccess.Open(importPath, Godot.FileAccess.ModeFlags.Read);
        if (file is null)
            return false;

        while (!file.EofReached())
        {
            var line = file.GetLine().Trim();
            if (!line.StartsWith(ImportPathPrefix) || !line.EndsWith('"'))
                continue;

            importedPath = line[ImportPathPrefix.Length..^1];
            return importedPath.Length > 0;
        }

        return false;
    }
}
