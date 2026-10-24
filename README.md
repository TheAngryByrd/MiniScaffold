# MiniScaffold

## What is MiniScaffold?

This is an [F# Template](https://docs.microsoft.com/en-us/dotnet/core/tools/custom-templates) for:

- creating and publishing [libraries](https://docs.microsoft.com/en-us/dotnet/standard/glossary#library) targeting .NET 8.0 `net8.0`
- creating and publishing [applications](https://docs.microsoft.com/en-us/dotnet/core/tutorials/cli-create-console-app#hello-console-app) with the target framework from the application project

## Why use MiniScaffold

This takes away the ambiguity that developers face when creating an OSS project. Such as:

- How do I structure my project?
- How do I create repeatable builds?
- Which test framework should I use?
- How do I create releases easily?
- What Issue/Pull Request templates should I use?
- How should I go about creating documentation?
- How do I get CI/CD setup easily?
- What's the standard .gitignore file?
- What other things would make it easier for me when starting off creating a project I might not even know about?

## What does this include in the box?


### All project types

- [Standard project structure](https://docs.microsoft.com/en-us/dotnet/core/porting/project-structure) for your dotnet application
- [Build Automation](https://en.wikipedia.org/wiki/Build_automation) tool via [FAKE](https://fake.build/)
- [Package management](https://en.wikipedia.org/wiki/Package_manager) tool via [Nuget](https://learn.microsoft.com/en-us/nuget/)
- [Unit Testing](https://en.wikipedia.org/wiki/Unit_testing) via [Expecto](https://github.com/haf/expecto)
- [Code Coverage](https://en.wikipedia.org/wiki/Code_coverage) via [Altcover](https://github.com/SteveGilham/altcover)
    - Also builds an html report with [ReportGenerator](https://github.com/danielpalme/ReportGenerator)
- [Code formatting](https://en.wikipedia.org/wiki/Programming_style) style via [Fantomas](https://github.com/fsprojects/fantomas)
- `GitRelease` build step commits latest [CHANGELOG.md](https://keepachangelog.com/en/1.0.0/) in the body and creates a [git tag](https://git-scm.com/book/en/v2/Git-Basics-Tagging).
    - If you [reference a Pull Request](https://github.com/TheAngryByrd/MiniScaffold/blob/master/CHANGELOG.md#0230-beta001---2020-02-07) in the `CHANGELOG.md` it will [update that Pull Request](https://github.com/TheAngryByrd/MiniScaffold/pull/186#ref-commit-b343218) with the version it was released in.
- `GitHubRelease` build step publishes a [GitHub Release](https://help.github.com/en/articles/creating-releases) via the  [CHANGELOG.md](https://keepachangelog.com/en/1.0.0/) and adds any artifacts (nuget/zip/targz/etc).
- [Continuous integration](https://en.wikipedia.org/wiki/Continuous_integration) via [GitHub Actions](https://github.com/features/actions)

### For [Libraries](Content/Library/README.md)
- Builds for `net8.0` - [Target Frameworks](https://docs.microsoft.com/en-us/dotnet/standard/frameworks)
    - To build for `net8.0` [.NET 8.0 ](https://dotnet.microsoft.com/download) installed
- [Sourcelink](https://github.com/dotnet/sourcelink) which enables a great source debugging experience for your users, by adding source control metadata to your built assets
- [Documentation Generation](https://github.com/fsprojects/FSharp.Formatting) - Generates Documentation from markdown files, fsx files, and the [XML Documentation Comments](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/xmldoc/) from your library.
- [Continuous deployment](https://en.wikipedia.org/wiki/Continuous_deployment) via [GitHub Actions](https://github.com/features/actions)
    - [Publish to NuGet] - Pushes NuGet packages to [NuGet](https://docs.microsoft.com/en-us/nuget/what-is-nuget)
        - Generates [Package Version](https://docs.microsoft.com/en-us/nuget/reference/nuspec#version) from `CHANGELOG.md`
        - Adds [Package Release Notes](https://docs.microsoft.com/en-us/nuget/reference/nuspec#releasenotes) metadata from `CHANGELOG.md`
    - [Documentation Publish] - Publish Documenation to [Github Pages](https://pages.github.com/)


### For [Applications](Content/Console/README.md)
- Basic argument parsing example via [Argu](https://fsprojects.github.io/Argu/)
- Builds the target framework from the application project.
- Uses `dotnet publish` to create framework-dependent applications for `win-x64`, `osx-x64`, and `linux-x64`.
- Creates three ZIP archives in `dist/` with names of the form `<product>-<version>-<runtime>.zip`.
- Places the executable and supporting files at each archive root.
- Requires the corresponding .NET runtime on the destination computer.

---

## Getting started quickly


### Install the [dotnet template](https://docs.microsoft.com/en-us/dotnet/core/tools/custom-templates) from NuGet:

```
dotnet new install "MiniScaffold::*"
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

The scaffold defaults to using the `main` branch for releases. If you release from a different branch, you can use the `--releaseBranch <branch name>` parameter to use your release branch instead of the default.

---

## Builds

GitHub Actions |
:---: |
[![GitHub Actions](https://github.com/TheAngryByrd/MiniScaffold/workflows/Build%20master/badge.svg)](https://github.com/TheAngryByrd/MiniScaffold/actions?query=branch%3Amaster) |
[![Build History](https://buildstats.info/github/chart/TheAngryByrd/MiniScaffold)](https://github.com/TheAngryByrd/MiniScaffold/actions?query=branch%3Amaster) |


## NuGet


Stable | Prerelease
:---: | :---:
[![NuGet Badge](https://buildstats.info/nuget/MiniScaffold)](https://www.nuget.org/packages/MiniScaffold/) | [![NuGet Badge](https://buildstats.info/nuget/MiniScaffold?includePreReleases=true)](https://www.nuget.org/packages/MiniScaffold/)

---

## Maintainer trusted publishing setup

The root workflow publishes the MiniScaffold template package through [NuGet trusted publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing).
Before a release, create a policy with the correct NuGet package owner and package scopes.
Use these GitHub policy fields:

| Field | Value |
| --- | --- |
| GitHub owner | `TheAngryByrd` |
| Repository | `MiniScaffold` |
| Workflow filename | `publish.yml` |
| Environment | `nuget` |

Add an environment variable named `NUGET_USER` to the existing `nuget` environment.
Use the policy creator's actual NuGet profile name, not an email address or an assumed GitHub username.
The NuGet package owner can be an organization with a different name.
`NuGet/login@v1` supplies a short-lived API key through the existing `NUGET_TOKEN` build input.
The workflow has no stored-key fallback.

After an authorized tag-triggered release, verify the NuGet package version and the GitHub release artifacts.
Check the complete run logs for credentials and credential fragments.
Check that NuGet reports the policy as active and permanent.
Only after all verification succeeds, remove the stored GitHub `NUGET_TOKEN` secret and revoke the old scoped NuGet key.
Keep both credentials until these checks pass.
See the [release guide](docsSrc/How_Tos/release-a-nuget-package.md) for failure checks and manual emergency recovery.

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
* [FSharp.Control.WebSockets](https://github.com/TheAngryByrd/FSharp.Control.WebSockets)
* [FSharp.Control.Redis.Streams](https://github.com/TheAngryByrd/FSharp.Control.Redis.Streams)
* [FsLibLog](https://github.com/TheAngryByrd/FsLibLog)
* [Giraffe.HotReload](https://github.com/baronfel/Giraffe.HotReload)
* [EFCore.FSharp](https://github.com/efcore/EFCore.FSharp)


#### This project uses the following projects:
* Heavily inspired by [Project Scaffold](https://github.com/fsprojects/ProjectScaffold)
* [Nuget](https://learn.microsoft.com/en-us/nuget/)
* [FAKE](https://fsharp.github.io/FAKE/)
* [Expecto](https://github.com/haf/expecto)
* [Buildstats.info](https://github.com/dustinmoris/CI-BuildStats)
* [Source Link](https://github.com/ctaggart/SourceLink)
* [AltCover](https://github.com/SteveGilham/altcover)
* [ReportGenerator](https://github.com/danielpalme/ReportGenerator)
* [Fantomas](https://github.com/fsprojects/fantomas)
* [Argu](https://github.com/fsprojects/Argu)
* [dotnet-packaging](https://github.com/qmfrederik/dotnet-packaging)
* [FSharp.Formatting](https://github.com/fsprojects/FSharp.Formatting)
