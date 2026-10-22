---
title: Console scaffold output
category: Reference
categoryindex: 4
index: 200
---

# Console scaffold output

The `--outputType console` parameter generates an F# console solution with build scripts, tests, and GitHub Actions workflows.

## Repository files

The generated repository includes these directories and files:

| Path | Purpose |
| --- | --- |
| `.config/` | The .NET tool manifest. |
| `.devcontainer/` | Development container configuration. |
| `.github/` | GitHub Actions workflows and issue and pull request templates. |
| `.vscode/` | Visual Studio Code settings and recommended extensions. |
| `build/` | FAKE build scripts. |
| `src/` | Application source code. |
| `tests/` | Test projects. |
| `build.cmd` | The Windows build command. |
| `build.sh` | The macOS and Linux build command. |
| `global.json` | The .NET SDK version and version selection policy. |
| `Directory.Build.props` | Shared project properties. |
| `Directory.Build.targets` | Shared build targets. |
| `Directory.Packages.props` | Central package versions. |
| `NuGet.config` | NuGet configuration. |
| `CHANGELOG.md` | Release history. |
| `README.md` | Project introduction and instructions. |
| `LICENSE.md` | License terms. |

For a solution named `MyCoolNewApp`, the solution file is `MyCoolNewApp.slnx`.
The application project is `src/MyCoolNewApp/MyCoolNewApp.fsproj`.
The test project is `tests/MyCoolNewApp.Tests/MyCoolNewApp.Tests.fsproj`.

## Packages

The default build target is `CreatePackages`.
This target builds and tests the solution before it creates packages in `dist/`.
The application project targets `net10.0`.
The packaging commands override the target framework with `net8.0`, as defined by `targetFramework` in `build/build.fs`.

| Runtime identifier | Package format |
| --- | --- |
| `win-x64` | `.zip` |
| `osx-x64` | `.tar.gz` |
| `linux-x64` | `.tar.gz` |
