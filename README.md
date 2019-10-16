# MiniScaffold
F# Template for
- creating and publishing [libraries](#Library) targeting .NET Full `net461` and Core `netstandard2.1`
- creating and publishing [applications](#Application) targeting .NET  Core `netcoreapp3.0`

## What does this include in the box?

Not so mini anymore...

### All project types

- [Standard project structure](https://docs.microsoft.com/en-us/dotnet/core/porting/project-structure) for your dotnet application
- [Build Automation](https://en.wikipedia.org/wiki/Build_automation) tool via [FAKE](https://fake.build/)
- [Package management](https://en.wikipedia.org/wiki/Package_manager) tool via [Paket](https://fsprojects.github.io/Paket/)
- [Unit Testing](https://en.wikipedia.org/wiki/Unit_testing) via [Expecto](https://github.com/haf/expecto)
- [Code Coverage](https://en.wikipedia.org/wiki/Code_coverage) via [Altcover](https://github.com/SteveGilham/altcover)
    - Also builds an html report with [ReportGenerator](https://github.com/danielpalme/ReportGenerator)
- [Code formatting](https://en.wikipedia.org/wiki/Programming_style) style via [Fantomas](https://github.com/fsprojects/fantomas)
- `Release` build step commits latest [Release Notes](https://fake.build/apidocs/v5/fake-core-releasenotes.html) in the body and creates a [git tag](https://git-scm.com/book/en/v2/Git-Basics-Tagging).
    - If you [reference a Pull Request](https://github.com/TheAngryByrd/FSharp.Control.WebSockets/blob/master/RELEASE_NOTES.md#021---2019-09-12) in the `Release Notes` it will [update that Pull Request](https://github.com/TheAngryByrd/FSharp.Control.WebSockets/pull/3#ref-commit-142baba) with the version it was released in.
- `Release` build step publishes a Github Release via the  [Release Notes](https://fake.build/apidocs/v5/fake-core-releasenotes.html) and adds any artifacts (nuget/zip/targz/etc).
- CI via [AppVeyor](https://www.appveyor.com/docs/) (Windows) and [TravisCI] (Linux)


### For [Libraries](Content/Library/README.md)
- Builds for both `net461` and `netstandard2.1` - [Target Frameworks](https://docs.microsoft.com/en-us/dotnet/standard/frameworks)
    - To build for `net461`
        - On windows: Have at least .NET Framework 4.6.1 installed
        - On osx/linux: Have [mono](https://www.mono-project.com/download/stable/) installed
    - To build for `netstandard2.1`
        - Have [.NET core 3.0](https://dotnet.microsoft.com/download) installed
- [Sourcelink](https://github.com/dotnet/sourcelink) which enables a great source debugging experience for your users, by adding source control metadata to your built assets
- [Release](#Library-Release) build step pushes NuGet packages to [NuGet](https://www.nuget.org/)


### For [Applications](Content/Console/README.md)
- Basic argument parsing example via [Argu](https://fsprojects.github.io/Argu/)
- Builds a `netcoreapp3.0` application - [Target Frameworks](https://docs.microsoft.com/en-us/dotnet/standard/frameworks)
    - To build for `netcoreapp3.0`
        - Have [.NET core 3.0](https://dotnet.microsoft.com/download) installed
- Builds for `win-x64`, `osx-x64` and `linux-x64` - [Runtime Identifiers](https://docs.microsoft.com/en-us/dotnet/core/rid-catalog).  Bundles the application via [dotnet-packaging](https://github.com/qmfrederik/dotnet-packaging)
    - Bundles the `win-x64` application in a .zip file.
    - Bundles the `osx-x64` application in a .tar.gz file.
    - Bundles the `linux-x64` application in a .tar.gz file.
---

## Getting started


### Install the [dotnet template](https://docs.microsoft.com/en-us/dotnet/core/tools/custom-templates) from nuget:

```
dotnet new -i "MiniScaffold::*"
```

Then choose:

- [I want to build a Library](Content/Library/README.md)
- [I want to build an Application](Content/Console/README.md)

---

## Builds

MacOS/Linux | Windows
:---: | :---:
[![Travis Badge](https://travis-ci.org/TheAngryByrd/MiniScaffold.svg?branch=master)](https://travis-ci.org/TheAngryByrd/MiniScaffold) | [![Build status](https://ci.appveyor.com/api/projects/status/rvwrjthtnew2digr/branch/master?svg=true)](https://ci.appveyor.com/project/TheAngryByrd/miniscaffold/branch/master)
[![Build History](https://buildstats.info/travisci/chart/TheAngryByrd/MiniScaffold)](https://travis-ci.org/TheAngryByrd/MiniScaffold/builds) | [![Build History](https://buildstats.info/appveyor/chart/TheAngryByrd/MiniScaffold)](https://ci.appveyor.com/project/TheAngryByrd/MiniScaffold)

## Nuget


Stable | Prerelease
:---: | :---:
[![NuGet Badge](https://buildstats.info/nuget/MiniScaffold)](https://www.nuget.org/packages/MiniScaffold/) | [![NuGet Badge](https://buildstats.info/nuget/MiniScaffold?includePreReleases=true)](https://www.nuget.org/packages/MiniScaffold/)

---

## Options

### githubUserName
This is uesd to atomatically configure author information in the nuget package, as well as configure push urls for repo locations.

### outputType
Defaults to Library

When set to either Console or Library project and the supporting infrastructure around their respective types.

---


## Known issues


```
-bash: ./build.sh: Permission denied
```

This is because dotnet template loses permissions of files. (https://github.com/TheAngryByrd/MiniScaffold/pull/37) added a post hook to address this but this only fixes it for dotnet sdk 2.x users.  dotnet sdk 1.x will need to run `chmod +x ./build.sh`


#### Example Projects using this template:
* [Chessie.Hopac](https://github.com/TheAngryByrd/Chessie.Hopac)
* [Marten.FSharp](https://github.com/TheAngryByrd/Marten.FSharp)


#### This project uses the following projects:
* [Paket](https://fsprojects.github.io/Paket/)
* [FAKE](https://fsharp.github.io/FAKE/)
* [Expecto](https://github.com/haf/expecto)
* Heavily inspired by [Project Scaffold](https://github.com/fsprojects/ProjectScaffold)
* [Buildstats.info](https://github.com/dustinmoris/CI-BuildStats)
* [SourceLink](https://github.com/ctaggart/SourceLink)
* [AltCover](https://github.com/SteveGilham/altcover)
* [ReportGenerator](https://github.com/danielpalme/ReportGenerator)
* [Fantomas](https://github.com/fsprojects/fantomas)
* [Argu](https://github.com/fsprojects/Argu)
* [dotnet-packaging](https://github.com/qmfrederik/dotnet-packaging)
