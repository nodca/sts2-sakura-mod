namespace SakuraMod.TestRunner;

public sealed record RuntimeWorkspace(
    string Root,
    string Executable,
    string ModsDirectory,
    string HomeDirectory,
    string DataDirectory,
    string ConfigDirectory,
    string CacheDirectory,
    string UserDataDirectory);

public static class RuntimeWorkspaceBuilder
{
    public static RuntimeWorkspace Create(
        RuntimePrerequisites prerequisites,
        string runRoot,
        string packageDirectory,
        string runtimeTestAssembly,
        string runtimeTestManifest)
    {
        var root = Path.Combine(runRoot, "runtime-root");
        if (Directory.Exists(root))
        {
            throw new InvalidOperationException($"Runtime root already exists: {root}");
        }

        Directory.CreateDirectory(root);
        foreach (var entry in new DirectoryInfo(prerequisites.GameRoot)
                     .EnumerateFileSystemInfos()
                     .OrderBy(value => value.Name, StringComparer.Ordinal))
        {
            if (entry.Name is "mods" or "mods_STEAMTEST" or "SlayTheSpire2")
            {
                continue;
            }

            var destination = Path.Combine(root, entry.Name);
            if (entry is DirectoryInfo)
            {
                Directory.CreateSymbolicLink(destination, entry.FullName);
            }
            else
            {
                File.CreateSymbolicLink(destination, entry.FullName);
            }
        }

        var executable = Path.Combine(root, "SlayTheSpire2");
        File.Copy(prerequisites.GameExecutable, executable);
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            File.SetUnixFileMode(executable, File.GetUnixFileMode(prerequisites.GameExecutable));
        }

        var mods = Path.Combine(root, "mods");
        Directory.CreateDirectory(mods);
        CopyDirectory(prerequisites.RitsuPackageRoot, Path.Combine(mods, "STS2-RitsuLib"));
        CopyDirectory(packageDirectory, Path.Combine(mods, "SakuraMod"));
        var runtimeModDirectory = Path.Combine(mods, "SakuraMod.RuntimeTests");
        Directory.CreateDirectory(runtimeModDirectory);
        File.Copy(runtimeTestAssembly, Path.Combine(runtimeModDirectory, "SakuraMod.RuntimeTests.dll"));
        File.Copy(runtimeTestManifest, Path.Combine(runtimeModDirectory, "SakuraMod.RuntimeTests.json"));

        var modDirectories = Directory.EnumerateDirectories(mods)
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var expected = new[] { "STS2-RitsuLib", "SakuraMod", "SakuraMod.RuntimeTests" }.Order(StringComparer.Ordinal);
        if (!modDirectories.SequenceEqual(expected, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"Isolated runtime mod set is not exact: {string.Join(", ", modDirectories)}");
        }

        var profileRoot = Path.Combine(runRoot, "profile");
        var home = Path.Combine(profileRoot, "home");
        var data = Path.Combine(profileRoot, "data");
        var config = Path.Combine(profileRoot, "config");
        var cache = Path.Combine(profileRoot, "cache");
        Directory.CreateDirectory(home);
        Directory.CreateDirectory(data);
        Directory.CreateDirectory(config);
        Directory.CreateDirectory(cache);
        return new RuntimeWorkspace(
            root,
            executable,
            mods,
            home,
            data,
            config,
            cache,
            Path.Combine(data, "SlayTheSpire2"));
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }
}
