# MiniScaffold

## What is MiniScaffold?

This is an [F# Template](https://docs.microsoft.com/en-us/dotnet/core/tools/custom-templates) for:

- creating and publishing [libraries](https://docs.microsoft.com/en-us/dotnet/standard/glossary#library) targeting .NET Full `net461` and Core `netstandard2.1`
- creating and publishing [applications](https://docs.microsoft.com/en-us/dotnet/core/tutorials/cli-create-console-app#hello-console-app) targeting .NET  Core `netcoreapp3.1`


## Why use MiniScaffold

This takes away the ambiguity that developers face when creating an OSS project. Such as:

- How do I structure my project?
- How do I create repeatable builds?
- Which test framework should I use?
- How to I create releases easily?
- What Issue/Pull Request templates should I use?
- How should I go about creating documentation?
- How do I get CI setup easily?
- What's the standard .gitignore file?
- What other things would make it easier for me when starting off creating a project I might not even know about?

## What does this include in the box?


### All project types

- [Standard project structure](https://docs.microsoft.com/en-us/dotnet/core/porting/project-structure) for your dotnet application
- [Build Automation](https://en.wikipedia.org/wiki/Build_automation) tool via [FAKE](https://fake.build/)
- [Package management](https://en.wikipedia.org/wiki/Package_manager) tool via [Paket](https://fsprojects.github.io/Paket/)
- [Unit Testing](https://en.wikipedia.org/wiki/Unit_testing) via [Expecto](https://github.com/haf/expecto)
- [Code Coverage](https://en.wikipedia.org/wiki/Code_coverage) via [Altcover](https://github.com/SteveGilham/altcover)
    - Also builds an html report with [ReportGenerator](https://github.com/danielpalme/ReportGenerator)
- [Code formatting](https://en.wikipedia.org/wiki/Programming_style) style via [Fantomas](https://github.com/fsprojects/fantomas)
- `Release` build step commits latest [RELEASE_NOTES.md](https://fake.build/apidocs/v5/fake-core-releasenotes.html) in the body and creates a [git tag](https://git-scm.com/book/en/v2/Git-Basics-Tagging).
    - If you [reference a Pull Request](https://github.com/TheAngryByrd/FSharp.Control.WebSockets/blob/master/RELEASE_NOTES.md#021---2019-09-12) in the `RELEASE_NOTES.md` it will [update that Pull Request](https://github.com/TheAngryByrd/FSharp.Control.WebSockets/pull/3#ref-commit-142baba) with the version it was released in.
- `Release` build step publishes a [GitHub Release](https://help.github.com/en/articles/creating-releases) via the  [RELEASE_NOTES.md](https://fake.build/apidocs/v5/fake-core-releasenotes.html) and adds any artifacts (nuget/zip/targz/etc).
- [Continuous integration](https://en.wikipedia.org/wiki/Continuous_integration) via [AppVeyor](https://www.appveyor.com/docs/) (Windows) and [TravisCI](https://docs.travis-ci.com/) (Linux) or [GitHub Actions](https://github.com/features/actions)


### For [Libraries](Content/Library/README.md)
- Builds for both `net461` and `netstandard2.1` - [Target Frameworks](https://docs.microsoft.com/en-us/dotnet/standard/frameworks)
    - To build for `net461`
        - On windows: Have at least .NET Framework 4.6.1 installed
        - On macOS/linux: Have [mono](https://www.mono-project.com/download/stable/) installed
    - To build for `netstandard2.1`
        - Have [.NET core 3.1](https://dotnet.microsoft.com/download) installed
- [Sourcelink](https://github.com/dotnet/sourcelink) which enables a great source debugging experience for your users, by adding source control metadata to your built assets
- [Documentation Generation](https://github.com/fsprojects/FSharp.Formatting) - Generates Documentation from markdown files, fsx files, and the [XML Documentation Comments](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/xmldoc/) from your library.
- [Release](Content/Library/README.md#Releasing) build step pushes NuGet packages to [NuGet](https://docs.microsoft.com/en-us/nuget/what-is-nuget)
    - Generates [Package Version](https://docs.microsoft.com/en-us/nuget/reference/nuspec#version) from `RELEASE_NOTES.md`
    - Adds [Package Release Notes](https://docs.microsoft.com/en-us/nuget/reference/nuspec#releasenotes) metadata from `RELEASE_NOTES.md`


### For [Applications](Content/Console/README.md)
- Basic argument parsing example via [Argu](https://fsprojects.github.io/Argu/)
- Builds a `netcoreapp3.1` application - [Target Frameworks](https://docs.microsoft.com/en-us/dotnet/standard/frameworks)
    - To build for `netcoreapp3.1`
        - Have [.NET core 3.1](https://dotnet.microsoft.com/download) installed
- Builds for `win-x64`, `osx-x64` and `linux-x64` - [Runtime Identifiers](https://docs.microsoft.com/en-us/dotnet/core/rid-catalog).  Bundles the application via [dotnet-packaging](https://github.com/qmfrederik/dotnet-packaging)
    - Bundles the `win-x64` application in a .zip file.
    - Bundles the `osx-x64` application in a .tar.gz file.
    - Bundles the `linux-x64` application in a .tar.gz file.

---

## Getting started quickly


### Install the [dotnet template](https://docs.microsoft.com/en-us/dotnet/core/tools/custom-templates) from NuGet:

```
dotnet new -i "MiniScaffold::*"
```

Then choose:

- [I want to build a Library](Content/Library/README.md)

```
dotnet new mini-scaffold -n MyCoolNewLib --githubUsername MyGithubUsername
```

- [I want to build an Application](Content/Console/README.md)

```
dotnet new mini-scaffold -n MyCoolNewApp --githubUsername MyGithubUsername -ou console
```

---

## Builds

macOS/Linux | Windows
:---: | :---:
[![Travis Badge](https://travis-ci.org/TheAngryByrd/MiniScaffold.svg?branch=master)](https://travis-ci.org/TheAngryByrd/MiniScaffold) | [![Build status](https://ci.appveyor.com/api/projects/status/rvwrjthtnew2digr/branch/master?svg=true)](https://ci.appveyor.com/project/TheAngryByrd/miniscaffold/branch/master)
[![Build History](https://buildstats.info/travisci/chart/TheAngryByrd/MiniScaffold)](https://travis-ci.org/TheAngryByrd/MiniScaffold/builds) | [![Build History](https://buildstats.info/appveyor/chart/TheAngryByrd/MiniScaffold)](https://ci.appveyor.com/project/TheAngryByrd/MiniScaffold)

## NuGet


Stable | Prerelease
:---: | :---:
[![NuGet Badge](https://buildstats.info/nuget/MiniScaffold)](https://www.nuget.org/packages/MiniScaffold/) | [![NuGet Badge](https://buildstats.info/nuget/MiniScaffold?includePreReleases=true)](https://www.nuget.org/packages/MiniScaffold/)

---

## Options

### githubUserName
This is used to automatically configure author information in the NuGet package, as well as configure push urls for repo locations.

### outputType
Defaults to Library

When set to either Console or Library project and the supporting infrastructure around their respective types.

---


## Known issues

### Permission denied

```
-bash: ./build.sh: Permission denied
```

This is because dotnet template loses permissions of files. (https://github.com/TheAngryByrd/MiniScaffold/pull/37) added a post hook to address this but this only fixes it for dotnet sdk 2.x users.  dotnet sdk 1.x will need to run `chmod +x ./build.sh`

### Appveyor badge not scaffolded correctly

Appveyor's conventions around generating URLs based on the project name has some nuances.  See [this issue](https://github.com/TheAngryByrd/MiniScaffold/issues/63) for details.


---

#### Example Projects using this template:
* [Chessie.Hopac](https://github.com/TheAngryByrd/Chessie.Hopac)
* [Marten.FSharp](https://github.com/TheAngryByrd/Marten.FSharp)


#### This project uses the following projects:
* [Paket](https://fsprojects.github.io/Paket/)
* [FAKE](https://fsharp.github.io/FAKE/)
* [Expecto](https://github.com/haf/expecto)
* Heavily inspired by [Project Scaffold](https://github.com/fsprojects/ProjectScaffold)
* [Buildstats.info](https://github.com/dustinmoris/CI-BuildStats)
* [Source Link](https://github.com/ctaggart/SourceLink)
* [AltCover](https://github.com/SteveGilham/altcover)
* [ReportGenerator](https://github.com/danielpalme/ReportGenerator)
* [Fantomas](https://github.com/fsprojects/fantomas)
* [Argu](https://github.com/fsprojects/Argu)
* [dotnet-packaging](https://github.com/qmfrederik/dotnet-packaging)
