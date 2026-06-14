using Godot;

namespace SakuraMod.SakuraModCode.Extensions;

//Mostly utilities to get asset paths.
public static class StringExtensions
{
    private const string ImportPathPrefix = "path=\"";

    public static string ImagePath(this string path)
    {
        return Path.Join(MainFile.ResPath, "images", path);
    }

    public static string CardImagePath(this string path)
    {
        path = Path.Join(MainFile.ResPath, "images", "card_portraits", path);
        if (TryResolveTexturePath(path, out var resolvedPath)) return resolvedPath;

        MainFile.Logger.Info("Could not find card image path: " + path);
        return ResolveDefaultTexturePath(Path.Join(MainFile.ResPath, "images", "card_portraits", "card.png"));
    }

    public static string BigCardImagePath(this string path)
    {
        path = Path.Join(MainFile.ResPath, "images", "card_portraits", "big", path);
        if (TryResolveTexturePath(path, out var resolvedPath)) return resolvedPath;

        MainFile.Logger.Info("Could not find big card image path: " + path);
        return ResolveDefaultTexturePath(Path.Join(MainFile.ResPath, "images", "card_portraits", "big", "card.png"));
    }

    public static string ClearCardImagePath(this string path)
    {
        path = path.ClearCardAssetPath();
        if (TryResolveTexturePath(path, out var resolvedPath)) return resolvedPath;

        MainFile.Logger.Info("Could not find clear card image path: " + path);
        return ResolveDefaultTexturePath(Path.Join(MainFile.ResPath, "images", "card_portraits", "big", "card.png"));
    }

    public static string ClearCardAssetPath(this string path)
    {
        return Path.Join(MainFile.ResPath, "images", "cards", "clear_cards", path);
    }

    public static string PowerImagePath(this string path)
    {
        path = Path.Join(MainFile.ResPath, "images", "powers", path);
        if (TryResolveTexturePath(path, out var resolvedPath)) return resolvedPath;

        MainFile.Logger.Info("Could not find power image path: " + path);
        return ResolveDefaultTexturePath(Path.Join(MainFile.ResPath, "images", "powers", "power.png"));
    }

    public static string BigPowerImagePath(this string path)
    {
        path = Path.Join(MainFile.ResPath, "images", "powers", "big", path);
        if (TryResolveTexturePath(path, out var resolvedPath)) return resolvedPath;

        MainFile.Logger.Info("Could not find big power image path: " + path);
        return ResolveDefaultTexturePath(Path.Join(MainFile.ResPath, "images", "powers", "big", "power.png"));
    }

    public static string RelicImagePath(this string path)
    {
        path = Path.Join(MainFile.ResPath, "images", "relics", path);
        if (TryResolveTexturePath(path, out var resolvedPath)) return resolvedPath;

        MainFile.Logger.Info("Could not find relic image path: " + path);
        return ResolveDefaultTexturePath(Path.Join(MainFile.ResPath, "images", "relics", "relic.png"));
    }

    public static string BigRelicImagePath(this string path)
    {
        path = Path.Join(MainFile.ResPath, "images", "relics", "big", path);
        if (TryResolveTexturePath(path, out var resolvedPath)) return resolvedPath;

        MainFile.Logger.Info("Could not find big relic image path: " + path);
        return ResolveDefaultTexturePath(Path.Join(MainFile.ResPath, "images", "relics", "big", "relic.png"));
    }

    public static string CharacterUiPath(this string path)
    {
        path = Path.Join(MainFile.ResPath, "images", "charui", path);
        return TryResolveTexturePath(path, out var resolvedPath) ? resolvedPath : path;
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
