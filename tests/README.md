# SakuraMod Test Layers

Run the layers independently from the repository root. Generated evidence is
written below `artifacts/tests/<run-id>/` and is ignored by Git.

The public-source export intentionally omits media, research inputs, and
private service tooling. In that checkout, tests requiring those inputs are
reported as skipped; in the private source tree, a missing input remains a
failure.

## Artifact retention

Remove stale local test evidence with:

```bash
scripts/cleanup-test-artifacts --target main
scripts/cleanup-test-artifacts --target main --delete
```

The default is a dry run. `--delete` removes every direct non-symlink run
directory older than 48 hours according to the UTC timestamp in its run ID. It
rechecks containment before removal and never follows symlinks.

## Fast tests

```bash
scripts/test-mod fast
```

Prerequisites: .NET 9 SDK plus the locally installed STS2 assemblies discovered
by `Sts2PathDiscovery.props`. This is the normal edit-cycle gate and usually
finishes in seconds. It runs discovered xUnit v3 tests without initializing
Godot, `ModelDb`, or a game process. A non-zero exit identifies the failed test
case; TRX output can be requested directly through `dotnet test` when needed.

## Package verification

```bash
scripts/test-mod package
```

Prerequisites: the fast-layer requirements and the configured Godot executable.
This creates a fresh isolated publish stage, requires a current non-empty DLL,
manifest, and PCK, validates their identities and hashes, mounts the PCK through
the isolated inspector, and rejects test-only packaged paths. It normally takes
tens of seconds. Inspect `package-result.json`, `pck-inventory.json`, and the
inspector/build logs when it fails; the command never accepts stale output.

## Runtime smoke

```bash
scripts/test-mod runtime
```

Prerequisites: the installed Linux STS2 runtime and matching RitsuLib package.
Steam must not already be running STS2. The command builds the package, creates
a temporary headless runtime with exactly RitsuLib, SakuraMod, and the test-only
host, and validates mod loading, versions, Harmony patches, `ModelDb`,
localization, representative resources, and the RitsuLib self-check. Expect
roughly one minute; the hard timeout is 120 seconds. Failures retain
`runtime-result.json`, `runner-result.json`, checkpoints, the game log, and the
isolated profile. Missing or malformed results, crashes, and timeouts all fail
closed.

## Combat and persistence

```bash
scripts/test-mod combat
scripts/test-mod combat --scenario save-load-restoration
```

查看完整入口和可用战斗场景：

```bash
scripts/test-mod --help
scripts/test-mod combat --help
```

`runtime`只执行固定的运行时冒烟场景；指定战斗场景必须使用
`scripts/test-mod combat --scenario <id>`。测试入口会拒绝其他层的多余参数，
避免参数被静默忽略后误跑默认场景。

Prerequisites are the same as runtime smoke. The aggregate command runs seven
fixed-seed semantic scenarios in fresh real-game processes: starter setup,
Extra Effect choice timing, Manifest and Temporary behavior, generated-card
pile memory, end-turn element cleanup, direct mid-combat save/load restoration,
and cross-combat cleanup. Each process has a 120-second hard timeout; the full
suite normally takes a few minutes.

The save/load scenario creates its own run, calls the native save operation
without leaving combat, exits that process, then starts a second process against
the same isolated profile and resumes the saved combat. No developer save or
committed save fixture is used. Choice assertions are semantic only; rendered
card fields and layout are outside this suite.

Every runtime layer fingerprints the real installed `mods/` directory and the
normal user-data directory before and after execution. Any change fails the run.
Each scenario has its own result, checkpoints, game log, semantic snapshots,
and protected-root fingerprints under the retained artifact directory.
