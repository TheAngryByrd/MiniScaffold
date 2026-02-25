# MiniScaffold

---

## What is MiniScaffold?


This is an [F# Template](https://docs.microsoft.com/en-us/dotnet/core/tools/custom-templates) for:

- creating and publishing [libraries](https://docs.microsoft.com/en-us/dotnet/standard/glossary#library) targeting .NET 8.0, 9.0, 10.0 (`net8.0`, `net9.0`, `net10.0`)
- creating and publishing [applications](https://docs.microsoft.com/en-us/dotnet/core/tutorials/cli-create-console-app#hello-console-app) targeting .NET 10.0 `net10.0`


## Why use MiniScaffold?

This takes away the ambiguity that developers face when creating an OSS project. Such as:

- How do I structure my project?
- How do I create repeatable builds?
- Which test framework should I use?
- How to I create releases?
- What Issue/Pull Request templates should I use?
- How should I go about creating documentation?
- How do I setup CI?
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
- `Release` build step commits latest [CHANGELOG.md](https://keepachangelog.com/en/1.0.0/) in the body and creates a [git tag](https://git-scm.com/book/en/v2/Git-Basics-Tagging).
    - If you [reference a Pull Request](https://github.com/TheAngryByrd/MiniScaffold/blob/master/CHANGELOG.md#0230-beta001---2020-02-07) in the `CHANGELOG.md` it will [update that Pull Request](https://github.com/TheAngryByrd/MiniScaffold/pull/186#ref-commit-b343218) with the version it was released in.
- `Release` build step publishes a [GitHub Release](https://help.github.com/en/articles/creating-releases) via the  [CHANGELOG.md](https://keepachangelog.com/en/1.0.0/) and adds any artifacts (nuget/zip/targz/etc).
- [Continuous integration](https://en.wikipedia.org/wiki/Continuous_integration) via [GitHub Actions](https://github.com/features/actions)


### For [Libraries](Content/Library/README.md)
- Builds for `net8.0`, `net9.0`, `net10.0` - [Target Frameworks](https://docs.microsoft.com/en-us/dotnet/standard/frameworks)
    - To build for `net8.0`, `net9.0`, `net10.0` have [.NET 10.0 SDK](https://dotnet.microsoft.com/download) installed
- [Sourcelink](https://github.com/dotnet/sourcelink) which enables a great source debugging experience for your users, by adding source control metadata to your built assets
- [Documentation Generation](https://github.com/fsprojects/FSharp.Formatting) - Generates Documentation from markdown files, fsx files, and the [XML Documentation Comments](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/xmldoc/) from your library.
- [Release](Content/Library/README.md#Releasing) build step pushes NuGet packages to [NuGet](https://docs.microsoft.com/en-us/nuget/what-is-nuget)
    - Generates [Package Version](https://docs.microsoft.com/en-us/nuget/reference/nuspec#version) from `CHANGELOG.md`
    - Adds [Package Release Notes](https://docs.microsoft.com/en-us/nuget/reference/nuspec#releasenotes) metadata from `CHANGELOG.md`


### For [Applications](Content/Console/README.md)
- Basic argument parsing example via [Argu](https://fsprojects.github.io/Argu/)
- Builds a `net10.0` application - [Target Frameworks](https://docs.microsoft.com/en-us/dotnet/standard/frameworks)
    - To build for `net10.0`
        - Have [.NET core 10.0](https://dotnet.microsoft.com/download) installed
- Builds for `win-x64`, `osx-x64` and `linux-x64` - [Runtime Identifiers](https://docs.microsoft.com/en-us/dotnet/core/rid-catalog).  Bundles the application via [dotnet-packaging](https://github.com/qmfrederik/dotnet-packaging)
    - Bundles the `win-x64` application in a .zip file.
    - Bundles the `osx-x64` application in a .tar.gz file.
    - Bundles the `linux-x64` application in a .tar.gz file.

---

## Getting started quickly

### Install the [dotnet template](https://docs.microsoft.com/en-us/dotnet/core/tools/custom-templates) from NuGet:

    [lang=bash]
    dotnet new -i "MiniScaffold::*"


#### I want to build a library

    [lang=bash]
    dotnet new mini-scaffold -n MyCoolNewLib --githubUsername MyGithubUsername

#### I want to build a console application

    [lang=bash]
    dotnet new mini-scaffold -n MyCoolNewApp --githubUsername MyGithubUsername -ou console


---

<div class="row row-cols-1 row-cols-md-2">
  <div class="col mb-4">
    <div class="card h-100">
      <div class="card-body">
        <h5 class="card-title">Tutorials</h5>
        <p class="card-text">Takes you by the hand through a series of steps to create your first library. </p>
      </div>
      <div class="card-footer text-right   border-top-0">
        <a href="{{root}}Tutorials/0-toc.html" class="btn btn-primary">Get started</a>
      </div>
    </div>
  </div>
  <div class="col mb-4">
    <div class="card h-100">
      <div class="card-body">
        <h5 class="card-title">How-To Guides</h5>
        <p class="card-text">Guides you through the steps involved in addressing key problems and use-cases. </p>
      </div>
      <div class="card-footer text-right   border-top-0">
        <a href="{{root}}/How_Tos/0-toc.html" class="btn btn-primary">Learn Usecases</a>
      </div>
    </div>
  </div>
  <div class="col mb-4 mb-md-0">
    <div class="card h-100">
      <div class="card-body">
        <h5 class="card-title">Explanations</h5>
        <p class="card-text">Discusses key topics and concepts at a fairly high level and provide useful background information and explanation..</p>
      </div>
      <div class="card-footer text-right   border-top-0">
        <a href="{{root}}Explanations/0-toc.html" class="btn btn-primary">Dive Deeper</a>
      </div>
    </div>
  </div>
  <div class="col">
    <div class="card h-100">
      <div class="card-body">
        <h5 class="card-title">Reference</h5>
        <p class="card-text">Contain technical references.</p>
      </div>
      <div class="card-footer text-right   border-top-0">
        <a href="{{root}}/Reference/0-toc.html" class="btn btn-primary">Read References</a>
      </div>
    </div>
  </div>
</div>
