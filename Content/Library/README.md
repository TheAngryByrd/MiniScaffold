# MyLib.1

[Enter useful description for MyLib.1]

---

## Builds

GitHub Actions |
:---: |
[![GitHub Actions](https://github.com/MyGithubUsername/MyLib.1/workflows/Build%20MyReleaseBranch/badge.svg)](https://github.com/MyGithubUsername/MyLib.1/actions?query=branch%3AMyReleaseBranch) |
[![Build History](https://buildstats.info/github/chart/MyGithubUsername/MyLib.1)](https://github.com/MyGithubUsername/MyLib.1/actions?query=branch%3AMyReleaseBranch) |

## NuGet

Package | Stable | Prerelease
--- | --- | ---
MyLib.1 | [![NuGet Badge](https://buildstats.info/nuget/MyLib.1)](https://www.nuget.org/packages/MyLib.1/) | [![NuGet Badge](https://buildstats.info/nuget/MyLib.1?includePreReleases=true)](https://www.nuget.org/packages/MyLib.1/)

---

### Developing

Make sure the following **requirements** are installed on your system:

- [dotnet SDK](https://www.microsoft.com/net/download/core) 10.0 or higher
- [Git LFS](https://git-lfs.com/) for handling binary assets

or

- [VSCode Dev Container](https://code.visualstudio.com/docs/remote/containers)

#### Git LFS Setup

This project uses Git LFS to handle binary assets efficiently. After cloning the repository, initialize Git LFS:

```sh
git lfs install
git lfs pull
```

The `.gitattributes` file is already configured to track binary files (images, documents, archives, etc.) with LFS automatically.


---

### Environment Variables

- `CONFIGURATION` will set the [configuration](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-build?tabs=netcore2x#options) of the dotnet commands.  If not set, it will default to Release.
  - `CONFIGURATION=Debug ./build.sh` will result in `-c` additions to commands such as in `dotnet build -c Debug`
- `ENABLE_COVERAGE` Will enable running code coverage metrics.  AltCover can have [severe performance degradation](https://github.com/SteveGilham/altcover/issues/57) so code coverage evaluation are disabled by default to speed up the feedback loop.
  - `ENABLE_COVERAGE=1 ./build.sh` will enable code coverage evaluation


---

### Building


```sh
> build.cmd <optional buildtarget> // on windows
$ ./build.sh  <optional buildtarget>// on unix
```

The bin of your library should look similar to:

```
$ tree src/MyLib.1/bin/
src/MyLib.1/bin/
└── Debug
    ├── net8.0
    │   ├── MyLib.1.deps.json
    │   ├── MyLib.1.dll
    │   ├── MyLib.1.pdb
    │   └── MyLib.1.xml
    ├── net9.0
    │   ├── MyLib.1.deps.json
    │   ├── MyLib.1.dll
    │   ├── MyLib.1.pdb
    │   └── MyLib.1.xml
    └── net10.0
        ├── MyLib.1.deps.json
        ├── MyLib.1.dll
        ├── MyLib.1.pdb
        └── MyLib.1.xml

```

---

### Build Targets

- `Clean` - Cleans artifact and temp directories.
- `DotnetRestore` - Runs [dotnet restore](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-restore?tabs=netcore2x) on the [solution file](https://docs.microsoft.com/en-us/visualstudio/extensibility/internals/solution-dot-sln-file?view=vs-2019).
- [`DotnetBuild`](#Building) - Runs [dotnet build](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-build?tabs=netcore2x) on the [solution file](https://docs.microsoft.com/en-us/visualstudio/extensibility/internals/solution-dot-sln-file?view=vs-2019).
- `FSharpAnalyzers` - Runs [BinaryDefense.FSharp.Analyzers](https://github.com/BinaryDefense/BinaryDefense.FSharp.Analyzers).
- `DotnetTest` - Runs [dotnet test](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-test?tabs=netcore21) on the [solution file](https://docs.microsoft.com/en-us/visualstudio/extensibility/internals/solution-dot-sln-file?view=vs-2019).
- `GenerateCoverageReport` - Code coverage is run during `DotnetTest` and this generates a report via [ReportGenerator](https://github.com/danielpalme/ReportGenerator).
- `ShowCoverageReport` - Shows the report generated in `GenerateCoverageReport`.
- `WatchTests` - Runs [dotnet watch](https://docs.microsoft.com/en-us/aspnet/core/tutorials/dotnet-watch?view=aspnetcore-3.0) with the test projects. Useful for rapid feedback loops.
- `GenerateAssemblyInfo` - Generates [AssemblyInfo](https://docs.microsoft.com/en-us/dotnet/api/microsoft.visualbasic.applicationservices.assemblyinfo?view=netframework-4.8) for libraries.
- `DotnetPack` - Runs [dotnet pack](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-pack). This includes running [Source Link](https://github.com/dotnet/sourcelink).
- `SourceLinkTest` - Runs a Source Link test tool to verify Source Links were properly generated.
- `PublishToNuGet` - Publishes the NuGet packages generated in `DotnetPack` to NuGet via [nuget push](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-nuget-push). Runs only from `Github Actions`.
- `GitRelease` - Creates a commit message with the [Release Notes](https://fake.build/apidocs/v5/fake-core-releasenotes.html) and a git tag via the version in the `Release Notes`.
- `GitHubRelease` - Publishes a [GitHub Release](https://help.github.com/en/articles/creating-releases) with the Release Notes and any NuGet packages. Runs only from `Github Actions`.
- `FormatCode` - Runs [Fantomas](https://github.com/fsprojects/fantomas) on the solution file.
- `CheckFormatCode` - Runs [Fantomas --check](https://fsprojects.github.io/fantomas/docs/end-users/FormattingCheck.html) on the solution file.
- `BuildDocs` - Generates [Documentation](https://fsprojects.github.io/FSharp.Formatting) from `docsSrc` and the [XML Documentation Comments](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/xmldoc/) from your libraries in `src`.
- `WatchDocs` - Generates documentation and starts a webserver locally.  It will rebuild and hot reload if it detects any changes made to `docsSrc` files, or libraries in `src`.

---


### Releasing

- [Start a git repo with a remote](https://help.github.com/articles/adding-an-existing-project-to-github-using-the-command-line/)

```sh
git init
git add .
git commit -m "Scaffold"
git branch -M MyReleaseBranch
git remote add origin https://github.com/MyGithubUsername/MyLib.1.git
git push -u origin MyReleaseBranch
```

- [Create an Environment](https://docs.github.com/en/actions/deployment/targeting-different-environments/using-environments-for-deployment#creating-an-environment) on your repository named `nuget`.
- [Create a NuGet API key](https://learn.microsoft.com/en-us/nuget/nuget-org/publish-a-package#create-an-api-key)
- Add your `NUGET_TOKEN` to the [Environment Secrets](https://docs.github.com/en/actions/deployment/targeting-different-environments/using-environments-for-deployment#environment-secrets) of your newly created environment.
- Then update the `CHANGELOG.md` with an "Unreleased" section containing release notes for this version, in [KeepAChangelog](https://keepachangelog.com/en/1.1.0/) format.

NOTE: Its highly recommend to add a link to the Pull Request next to the release note that it affects. The reason for this is when the `RELEASE` target is run, it will add these new notes into the body of git commit. GitHub will notice the links and will update the Pull Request with what commit referenced it saying ["added a commit that referenced this pull request"](https://github.com/TheAngryByrd/MiniScaffold/pull/179#ref-commit-837ad59). Since the build script automates the commit message, it will say "Bump Version to x.y.z". The benefit of this is when users goto a Pull Request, it will be clear when and which version those code changes released. Also when reading the `CHANGELOG`, if someone is curious about how or why those changes were made, they can easily discover the work and discussions.

Here's an example of adding an "Unreleased" section to a `CHANGELOG.md` with a `0.1.0` section already released.

```markdown
## [Unreleased]

### Added
- Does cool stuff!

### Fixed
- Fixes that silly oversight

## [0.1.0] - 2017-03-17
First release

### Added
- This release already has lots of features

[Unreleased]: https://github.com/MyGithubUsername/MyLib.1/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/MyGithubUsername/MyLib.1/releases/tag/v0.1.0
```

- You can then use the `GitRelease` target, specifying the version number either in the `RELEASE_VERSION` environment
  variable, or else as a parameter after the target name.  This will:
  - update `CHANGELOG.md`, moving changes from the `Unreleased` section into a new `0.2.0` section
    - if there were any prerelease versions of 0.2.0 in the changelog, it will also collect their changes into the final 0.2.0 entry
  - make a commit bumping the version:  `Bump version to 0.2.0` and adds the new changelog section to the commit's body
  - push a git tag

macOS/Linux Parameter:

```sh
./build.sh Release 0.2.0
```

macOS/Linux Environment Variable:

```sh
RELEASE_VERSION=0.2.0 ./build.sh Release
```

- The [Github Action](https://github.com/MyGithubUsername/MyLib.1/blob/MyReleaseBranch/.github/workflows/publish.yml) will handle the new tag:
  - publish the package to NuGet
  - create a GitHub release for that git tag, upload release notes and NuGet packages to GitHub


### Releasing Documentation

- Set Source for "Build and deployment" on [GitHub Pages](https://github.com/MyGithubUsername/MyLib.1/settings/pages) to `GitHub Actions`.
- Documentation is auto-deployed via [Github Action](https://github.com/MyGithubUsername/MyLib.1/blob/MyReleaseBranch/.github/workflows/fsdocs-gh-pages.yml) to [Your GitHub Page](https://MyGithubUsername.github.io/MyLib.1/)
