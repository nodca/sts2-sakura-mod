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

This mod requires RitsuLib. Install STS2-RitsuLib first and keep it enabled alongside
SakuraMod.

## Development Docs

The public install and build flow is kept here. Current domain language,
implementation contracts, local tool versions, and validation rules live in
[CONTEXT.md](CONTEXT.md), [docs/README.md](docs/README.md), and the
[Trellis specification map](.trellis/spec/index.md). The pinned package and
game versions are maintained in the project file and runtime safety spec,
rather than duplicated in this README.

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

Open the project with the same pinned Godot 4.5.1 editor used by the publish
pipeline:

```bash
scripts/godot-editor
```

Do not open `project.godot` through the system file association unless that
association also points to Godot 4.5.1. The project feature version does not
select the editor executable.

## Build

Compile C# and copy the DLL/manifest into the local STS2 mods folder:

```bash
dotnet build SakuraMod.csproj
```

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
