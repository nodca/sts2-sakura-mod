# SakuraMod

Slay the Spire 2 character mod for a Sakura-themed character.

## Install

Download the latest release zip from GitHub Releases, then extract the
`SakuraMod` folder into your Slay the Spire 2 `mods` directory.

The installed folder should contain:

```text
mods/SakuraMod/SakuraMod.dll
mods/SakuraMod/SakuraMod.json
mods/SakuraMod/SakuraMod.pck
```

This mod requires BaseLib. Install BaseLib first and keep it enabled alongside
SakuraMod.

## Local Baseline

- Slay the Spire 2: `v0.107.1` on this machine
- Godot: `4.5.1.stable.mono`
- .NET SDK: `9.0.117`
- Template package: `Alchyr.Sts2.Templates 2.4.3`
- Runtime BaseLib: `3.3.0`
- Mod id / assembly: `SakuraMod`

## Local Setup

Install the template if it is not already installed:

```bash
dotnet new install Alchyr.Sts2.Templates
```

Create a local `Directory.Build.props` from the example and set `GodotPath` for
your machine. `Directory.Build.props` is intentionally ignored by Git because it
contains machine-local paths.

```bash
cp Directory.Build.props.example Directory.Build.props
```

The template discovers the STS2 install path automatically on Linux, macOS, and
Windows. If discovery fails, set `Sts2Path` in `Directory.Build.props`.

## Build

Compile C# and copy the DLL/manifest into the local STS2 mods folder:

```bash
dotnet build SakuraMod.csproj
```

On this machine, .NET SDK 9.0.117 can emit `CS9057` from
`Alchyr.Sts2.ModAnalyzers` because the analyzer was built against a newer
compiler. The build still succeeds. Installing a .NET 10 SDK should remove that
environment warning.

Export the Godot resource pack and copy it into the local STS2 mods folder:

```bash
dotnet publish SakuraMod.csproj
```

After publish, the local game mods folder should contain:

```text
mods/SakuraMod/SakuraMod.dll
mods/SakuraMod/SakuraMod.json
mods/SakuraMod/SakuraMod.pck
```

## Project Layout

- `SakuraModCode/` contains C# runtime code.
- `SakuraMod/` contains exported resources and localization.
- Public source exports may keep `SakuraMod/images` as placeholder directories.
  Release zips include the runtime `.pck` used by the game.
