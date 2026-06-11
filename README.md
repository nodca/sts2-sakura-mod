# SakuraMod

Slay the Spire 2 character mod scaffold for a Sakura-themed character.

This repository is initialized from Alchyr's `alchyrsts2charmod` template and is
intended to use BaseLib for STS2 content registration.

## Local Baseline

- Slay the Spire 2: `v0.103.2` on this machine
- Godot: `4.5.1.stable.mono`
- .NET SDK: `9.0.117`
- Template package: `Alchyr.Sts2.Templates 2.4.3`
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
- `.trellis/spec/` contains the project-specific development guidelines future
  Trellis tasks should load before implementation.

## Asset Policy

Do not commit copyrighted character art, audio, or text unless the user has
explicitly supplied or approved the asset source and intended usage.

This public source export intentionally omits runtime image assets. The
`SakuraMod/images` directories are kept as placeholders so source layout remains
visible without redistributing private or copyrighted art.
