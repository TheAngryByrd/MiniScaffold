# Library output

`--outputType library` (the default) copies `Content/Library/` into a complete library repository.
`docsSrc/Reference/Library_Output.md` is the human reference for the generated files.

## Layout

- `src/MyLib.1/` - the library. Targets `$(LibraryTargetFrameworks)`, which is net10.0 and net11.0.
- `tests/MyLib.1.Tests/` - Expecto tests on `$(LibraryTargetFrameworks)`, with AltCover coverage. `dotnet test` runs the tests once for each framework.
- `build/` - `build.fs`, `Changelog.fs`, `FsDocs.fs`, and `build.fsproj` (net10.0).
- `docsSrc/` - fsdocs source for the generated documentation site.
- `.github/workflows/` - `build.yml`, `publish.yml`, and `fsdocs-gh-pages.yml`.
- `MyLib.1.slnx`, `Directory.Packages.props` (central package management), and `global.json` (SDK `11.0.100`).

## FAKE target graph

`Content/Library/build/build.fs` declares these chains. The default target is `DotnetPack`.

```fsharp
"DotnetRestore"
=?> ("CheckFormatCode", isCI.Value)
==> "DotnetBuild"
==> "DotnetTest"
==> "DotnetPack"
==> "PublishToNuGet"
==> "GitHubRelease"
==>! "Publish"
```

```mermaid
flowchart LR
    DotnetRestore -->|CI only| CheckFormatCode --> DotnetBuild
    DotnetRestore --> DotnetBuild --> DotnetTest --> DotnetPack --> PublishToNuGet --> GitHubRelease --> Publish
    UpdateChangelog --> GitRelease --> Release
    DotnetTest --> GenerateCoverageReport --> ShowCoverageReport
    CleanDocsCache --> BuildDocs
    DotnetBuild --> BuildDocs
    DotnetBuild --> WatchDocs
    Clean --> DotnetPack
```

`SourceLinkTest`, `FormatCode`, `RevertChangelog`, and `DeleteChangelogBackupFile` are outside these chains.
The source states: `// Do NOT put this in the dependency chain` for `RevertChangelog` and `DeleteChangelogBackupFile`.

## Release contract

1. A maintainer runs `./build.sh Release 0.2.0` locally (`Content/Library/README.md`).
2. `UpdateChangelog` moves `Unreleased` items into a new entry. `GitRelease` checks the branch, commits "Bump version to X", and pushes tag `v{version}`.
3. `publish.yml` runs on `v*` tags in the `nuget` environment with `id-token: write`.
4. `NuGet/login@v1` exchanges the OIDC token for a short-lived key that the job passes as `NUGET_TOKEN`.
5. `./build.sh Publish` runs `PublishToNuGet` and `GitHubRelease`.

`allPublishChecks` guards both publish targets: CI only, a non-empty changelog, and `RELEASE_TAG` equal to `v{NuGetVersion}` (ordinal compare).
The same contract as the root build applies. See [../build/release-and-publish.md](../build/release-and-publish.md).

## Target frameworks

The root `Directory.Build.props` defines one property for the library and its tests:

```xml
<LibraryTargetFrameworks>net10.0;net11.0</LibraryTargetFrameworks>
```

```mermaid
flowchart LR
    Props["Directory.Build.props LibraryTargetFrameworks"] --> Lib["src/MyLib.1 TargetFrameworks"]
    Props --> Tests["tests/MyLib.1.Tests TargetFrameworks"]
    Lib --> Nupkg["lib/net10.0 and lib/net11.0"]
    Tests --> Trx["one TRX file for each framework"]
```

- Change the TFM list of the generated library only in this property, so CI tests each packed framework on its own runtime.
- `ProjLib` keeps a literal `net10.0;net11.0`, because a console host does not define `LibraryTargetFrameworks`. `ProjTest` targets `net10.0` only, so the `net11.0` assembly of a `projLib` project is packed but not tested. The contract test "library projects share their target frameworks" checks that the library and its tests read the property.
- FSharp.Core stays at `10.0.102` in `Directory.Packages.props`. The package has one dependency group for each TFM, and each group has one FSharp.Core dependency at that version.
- `global.json` pins SDK `11.0.100`, because building `net11.0` needs SDK 11 or later. `build/build.fsproj` targets net10.0 (LTS).

## Build settings

- Fantomas runs on each build through `Directory.Build.targets`. Set `FormatOnBuild=false` to skip it.
- `src/Directory.Build.props` enables `TreatWarningsAsErrors` and `Nullable`, and adds the Ionide and G-Research analyzers with `GeneratePathProperty="true"`.
- FSharp.Analyzers.Build runs the analyzers after each build with the `$(PkgIonide_Analyzers)` and `$(PkgG-Research_FSharp_Analyzers)` paths (`src/Directory.Build.targets`). Analyzer findings do not fail the build.
- `GenerateCoverageReport` writes the HTML report to `docs/coverage`. `Content/Library/docsSrc/index.md` has no link to it ([issue #226](https://github.com/TheAngryByrd/MiniScaffold/issues/226)).
- With `AltCover=true`, `tests/Directory.Build.props` sets `TestTfmsInParallel=false`. AltCover 8 deletes every `$(TEMP)/*.altcover.runsettings` after each test run, so a parallel run of the other TFM can lose its open settings file (`MSB3061 ... Access to the path ... is denied` on Windows). AltCover 9 deletes only its own file.
- Coverage is opt-in through `ENABLE_COVERAGE`. The `publish.yml` comment states: "AltCover doesn't work with Release builds".
- `Directory.Packages.props` sets `RestorePackagesWithLockFile` and `RestoreLockedMode`. The template ships no `packages.lock.json`, so the first restore creates it.
- The generated `build.yml` runs Debug and Release on ubuntu, windows, and macOS, plus a devcontainer job.
- The generated workflows and devcontainer use the versions in [../ci/action-versions.md](../ci/action-versions.md).
- `fsdocs-gh-pages.yml` runs `./build.sh builddocs` and deploys `docs/` to GitHub Pages.

## Related

- [template-configuration.md](template-configuration.md)
- [console-output.md](console-output.md)
- [../tests/policy-tests.md](../tests/policy-tests.md) - GeneratedReleasePolicy and TrustedPublishing tests check this output.
