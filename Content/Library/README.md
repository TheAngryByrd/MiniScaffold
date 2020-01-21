# MyLib.1

[Enter useful description for MyLib.1]

---

## Builds

macOS/Linux | Windows
--- | ---
[![Travis Badge](https://travis-ci.org/MyGithubUsername/MyLib.1.svg?branch=master)](https://travis-ci.org/MyGithubUsername/MyLib.1) | [![Build status](https://ci.appveyor.com/api/projects/status/github/MyGithubUsername/MyLib.1?svg=true)](https://ci.appveyor.com/project/MyGithubUsername/MyLib.1)
[![Build History](https://buildstats.info/travisci/chart/MyGithubUsername/MyLib.1)](https://travis-ci.org/MyGithubUsername/MyLib.1/builds) | [![Build History](https://buildstats.info/appveyor/chart/MyGithubUsername/MyLib.1)](https://ci.appveyor.com/project/MyGithubUsername/MyLib.1)  

## NuGet 

Package | Stable | Prerelease
--- | --- | ---
MyLib.1 | [![NuGet Badge](https://buildstats.info/nuget/MyLib.1)](https://www.nuget.org/packages/MyLib.1/) | [![NuGet Badge](https://buildstats.info/nuget/MyLib.1?includePreReleases=true)](https://www.nuget.org/packages/MyLib.1/)

---

### Developing

Make sure the following **requirements** are installed on your system:

- [dotnet SDK](https://www.microsoft.com/net/download/core) 3.0 or higher
- [Mono](http://www.mono-project.com/) if you're on Linux or macOS.

or

- [VSCode Dev Container](https://code.visualstudio.com/docs/remote/containers)


---

### Environment Variables

- `CONFIGURATION` will set the [configuration](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-build?tabs=netcore2x#options) of the dotnet commands.  If not set, it will default to Release.
  - `CONFIGURATION=Debug ./build.sh` will result in `-c` additions to commands such as in `dotnet build -c Debug`
- `GITHUB_TOKEN` will be used to upload release notes and Nuget packages to GitHub.
  - Be sure to set this before releasing
- `DISABLE_COVERAGE` Will disable running code coverage metrics.  AltCover can have [severe performance degradation](https://github.com/SteveGilham/altcover/issues/57) so it's worth disabling when looking to do a quicker feedback loop.
  - `DISABLE_COVERAGE=1 ./build.sh`


---

### Building


```sh
> build.cmd <optional buildtarget> // on windows
$ ./build.sh  <optional buildtarget>// on unix
```

The bin of your library should look similar to:

```
$ tree src/MyCoolNewLib/bin/
src/MyCoolNewLib/bin/
└── Debug
    ├── net461
    │   ├── FSharp.Core.dll
    │   ├── MyCoolNewLib.dll
    │   ├── MyCoolNewLib.pdb
    │   ├── MyCoolNewLib.xml
    └── netstandard2.1
        ├── MyCoolNewLib.deps.json
        ├── MyCoolNewLib.dll
        ├── MyCoolNewLib.pdb
        └── MyCoolNewLib.xml

```

---

### Build Targets

- `Clean` - Cleans artifact and temp directories.
- `DotnetRestore` - Runs [dotnet restore](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-restore?tabs=netcore2x) on the [solution file](https://docs.microsoft.com/en-us/visualstudio/extensibility/internals/solution-dot-sln-file?view=vs-2019).
- [`DotnetBuild`](#Building) - Runs [dotnet build](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-build?tabs=netcore2x) on the [solution file](https://docs.microsoft.com/en-us/visualstudio/extensibility/internals/solution-dot-sln-file?view=vs-2019).
- `DotnetTest` - Runs [dotnet test](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-test?tabs=netcore21) on the [solution file](https://docs.microsoft.com/en-us/visualstudio/extensibility/internals/solution-dot-sln-file?view=vs-2019).
- `GenerateCoverageReport` - Code coverage is run during `DotnetTest` and this generates a report via [ReportGenerator](https://github.com/danielpalme/ReportGenerator).
- `WatchTests` - Runs [dotnet watch](https://docs.microsoft.com/en-us/aspnet/core/tutorials/dotnet-watch?view=aspnetcore-3.0) with the test projects. Useful for rapid feedback loops.
- `GenerateAssemblyInfo` - Generates [AssemblyInfo](https://docs.microsoft.com/en-us/dotnet/api/microsoft.visualbasic.applicationservices.assemblyinfo?view=netframework-4.8) for libraries.
- `DotnetPack` - Runs [dotnet pack](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-pack). This includes running [Source Link](https://github.com/dotnet/sourcelink).
- `SourceLinkTest` - Runs a Source Link test tool to verify Source Links were properly generated.
- `PublishToNuGet` - Publishes the NuGet packages generated in `DotnetPack` to NuGet via [paket push](https://fsprojects.github.io/Paket/paket-push.html).
- `GitRelease` - Creates a commit message with the [Release Notes](https://fake.build/apidocs/v5/fake-core-releasenotes.html) and a git tag via the version in the `Release Notes`.
- `GitHubRelease` - Publishes a [GitHub Release](https://help.github.com/en/articles/creating-releases) with the Release Notes and any NuGet packages.
- `FormatCode` - Runs [Fantomas](https://github.com/fsprojects/fantomas) on the solution file.
- `BuildDocs` - Generates Documentation from `docsSrc` and the [XML Documentation Comments](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/xmldoc/) from your libraries in `src`.
- `WatchDocs` - Generates documentation and starts a webserver locally.  It will rebuild and hot reload if it detects any changes made to `docsSrc` files, libraries in `src`, or the `docsTool` itself.
- `ReleaseDocs` - Will stage, commit, and push docs generated in the `BuildDocs` target.
- [`Release`](#Releasing) - Task that runs all release type tasks such as `PublishToNuGet`, `GitRelease`, `ReleaseDocs`, and `GitHubRelease`. Make sure to read [Releasing](#Releasing) to setup your environment correctly for releases.
---


### Releasing

- [Start a git repo with a remote](https://help.github.com/articles/adding-an-existing-project-to-github-using-the-command-line/)

```sh
git add .
git commit -m "Scaffold"
git remote add origin https://github.com/user/MyCoolNewLib.git
git push -u origin master
```

- [Add your NuGet API key to paket](https://fsprojects.github.io/Paket/paket-config.html#Adding-a-NuGet-API-key)

```sh
paket config add-token "https://www.nuget.org" 4003d786-cc37-4004-bfdf-c4f3e8ef9b3a
```

- [Create a GitHub OAuth Token](https://help.github.com/articles/creating-a-personal-access-token-for-the-command-line/)
  - You can then set the `GITHUB_TOKEN` to upload release notes and artifacts to github
  - Otherwise it will fallback to username/password

- Then update the `CHANGELOG.md` and add a new header with a version number and date just under the "Unreleased" header,
  in [KeepAChangelog](https://keepachangelog.com/en/1.1.0/) format. Don't forget to make the version number
  a link and update the link reference section at the bottom of the file, too. E.g., change this:

```markdown
## Unreleased
### Added
- FEATURE: Does cool stuff!

### Fixed
- BUGFIX: Fixes that silly oversight

## [0.1.0] - 2017-03-17
First release

### Added
- This release already has lots of features

[0.1.0]: https://github.com/user/MyCoolNewLib.git/releases/tag/v0.1.0
```

to this:

```markdown
## Unreleased

## [0.2.0] - 2017-04-20
### Added
- FEATURE: Does cool stuff!

### Fixed
- BUGFIX: Fixes that silly oversight

## [0.1.0] - 2017-03-17
First release

### Added
- This release already has lots of features

[0.2.0]: https://github.com/user/MyCoolNewLib.git/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/user/MyCoolNewLib.git/releases/tag/v0.1.0
```

- You can then use the `Release` target.  This will:
  - make a commit bumping the version:  `Bump version to 0.2.0` and add the changelog to the commit
  - publish the package to NuGet
  - push a git tag

```sh
./build.sh Release
```


