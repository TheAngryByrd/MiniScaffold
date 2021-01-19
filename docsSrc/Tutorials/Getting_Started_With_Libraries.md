# Getting Started

In this tutorial, we'll take a look at getting started with MiniScaffold and publishing your first library to NuGet and GitHub

## Prerequisites

- Install [git](https://git-scm.com/download)
- Install [.Net core](https://dotnet.microsoft.com/download)
- If on macOS or Linux, install [Mono](https://www.mono-project.com/download/stable/)
- If on Windows [ensure](https://docs.microsoft.com/en-us/dotnet/framework/migration-guide/how-to-determine-which-versions-are-installed) you have at least .NET Full Framework 4.6.1 installed
- Recommended IDE is [VSCode with Ionide](https://docs.microsoft.com/en-us/dotnet/fsharp/get-started/install-fsharp#install-f-with-visual-studio-code)
- Create a [NuGet account](https://www.nuget.org/)
- Create a [GitHub account](https://github.com/)

## Installing MiniScaffold

Install the [dotnet template](https://docs.microsoft.com/en-us/dotnet/core/tools/custom-templates) from NuGet:

    [lang=bash]
    dotnet new -i "MiniScaffold::*"

Then use the template to create your own library.  Replace `MyCoolNewLib` with your own library name and `MyGithubUsername` with your own GitHub name. If you have trouble picking a library name then [generate one](https://colinmorris.github.io/rbm/repos/).

    [lang=bash]
    dotnet new mini-scaffold -n MyCoolNewLib --githubUsername MyGithubUsername
    cd MyCoolNewLib

This will generate a structure similar to this

    [lang=bash]
    .
    |-- Directory.Build.props
    |-- LICENSE.md
    |-- MyCoolNewLib.sln
    |-- README.md
    |-- CHANGELOG.md
    |-- build.cmd
    |-- build.fsx
    |-- build.sh
    |-- docsSrc
    |   |-- Explanations
    |   |   |-- Background.md
    |   |-- How_Tos
    |   |   |-- Doing_A_Thing.md
    |   |   |-- Doing_Another_Thing.md
    |   |-- Tutorials
    |   |   |-- Getting_Started.md
    |   |-- content
    |   |   |-- hotload.js
    |   |   |-- style.css
    |   |   |-- submenu.js
    |   |   |-- tips.js
    |   |-- files
    |   |   |-- placeholder.md
    |   |-- index.md
    |-- docsTool
    |   |-- CLI.fs
    |   |-- Prelude.fs
    |   |-- Program.fs
    |   |-- README.md
    |   |-- docsTool.fsproj
    |   |-- paket.references
    |   |-- templates
    |       |-- helpers.fs
    |       |-- master.fs
    |       |-- modules.fs
    |       |-- namespaces.fs
    |       |-- nav.fs
    |       |-- partMembers.fs
    |       |-- partNested.fs
    |       |-- types.fs
    |-- paket.dependencies
    |-- paket.lock
    |-- src
    |   |-- Directory.Build.props
    |   |-- MyCoolNewLib
    |       |-- AssemblyInfo.fs
    |       |-- Library.fs
    |       |-- MyCoolNewLib.fsproj
    |       |-- paket.references
    |-- tests
        |-- Directory.Build.props
        |-- MyCoolNewLib.Tests
            |-- AssemblyInfo.fs
            |-- Main.fs
            |-- MyCoolNewLib.Tests.fsproj
            |-- Tests.fs
            |-- paket.references

This may look overwhelming, but we don't have to worry about all of these yet.  Let's just focus on the real important ones for this tutorial.

- `./src/MyCoolNewLib` - This is where your library's source code will live.
- `./tests/MyCoolNewLib.Tests` - This is where your library's test code will live.
- `build.cmd` or `build.sh` - Platform specific entry points into your `build.fsx` file.
- `build.fsx` - The main build script of your repository.
- `README.md` - The text file that introduces and explains a project.
- `CHANGELOG.md` - Text file containing versioning, date, and release notes.

## Building your library

To make sure everything is functioning correctly, run the build command for your platform:

    [lang=bash]
    ./build.sh //for macOS or Linux
    .\build.cmd \\for Windows

You'll be flooded with a screen full of text, documenting the build processes at it currently stands. If it completes successfully you should be greeted with something similar:

    [lang=bash]
    ---------------------------------------------------------------------
    Build Time Report
    ---------------------------------------------------------------------
    Target                   Duration
    ------                   --------
    Clean                    00:00:00.0180266
    DotnetRestore            00:00:08.1013232
    DotnetBuild              00:00:09.2786467
    DotnetTest               00:00:07.9084110
    GenerateCoverageReport   00:00:00.8499974
    DotnetPack               00:00:06.3346539
    Total:                   00:00:32.6192299
    Status:                  Ok
    ---------------------------------------------------------------------

This is FAKE telling us everything that ran, how long, and if it completed successfully.

If it does not complete successfully, either [open an issue](https://github.com/TheAngryByrd/MiniScaffold/issues) or ask on [F# Slack](https://fsharp.org/guides/slack/).


## Fill out README.md

The README.md comes with a lot of information but it's recommended to fill out the introductionary description.

## Making a Release

The release process is streamlined so you only have to start your git repository, set your NuGet and GitHub authorization keys, create `CHANGELOG` notes, and run the `Release` build target.

- [Create a GitHub Repository](https://help.github.com/en/github/getting-started-with-github/create-a-repo) and
[Start a git repo with your GitHub repository as a remote](https://help.github.com/articles/adding-an-existing-project-to-github-using-the-command-line/)


    [lang=bash]
    git add .
    git commit -m "Scaffold"
    git remote add origin https://github.com/MyGithubUsername/MyCoolNewLib.git
    git push -u origin master

- [Create a NuGet API key](https://docs.microsoft.com/en-us/nuget/nuget-org/publish-a-package#create-api-keys)

    - [Add your NuGet API key to paket](https://fsprojects.github.io/Paket/paket-config.html#Adding-a-NuGet-API-key)
    - or set the environment variable `NUGET_TOKEN` to your key

- [Create a GitHub OAuth Token](https://help.github.com/articles/creating-a-personal-access-token-for-the-command-line/)
  - You can then set the `GITHUB_TOKEN` to upload `CHANGELOG` notes and artifacts to github
    - If you're on a linux, you can put this key into your [.bashrc](https://unix.stackexchange.com/questions/129143/what-is-the-purpose-of-bashrc-and-how-does-it-work)
  - Otherwise it will fallback to username/password

Then update the `CHANGELOG.md` with an "Unreleased" section containing release notes for this version, in [KeepAChangelog](https://keepachangelog.com/en/1.1.0/) format.

<div class="alert alert-primary" role="alert">
    NOTE: Its highly recommend to add a link to the Pull Request next to the release note that it affects. The reason for this is when the <code>RELEASE</code> target is run, it will add these new notes into the body of git commit. GitHub will notice the links and will update the Pull Request with what commit referenced it saying <a href="https://github.com/TheAngryByrd/MiniScaffold/pull/179#ref-commit-837ad59">"added a commit that referenced this pull request"</a>. Since we automate the commit message, it will say "Bump Version to x.y.z". The benefit of this is when users goto a Pull Request, it will be clear when and which version those code changes released. Also when reading the <code>CHANGELOG</code>, if someone is curious about how or why those changes were made, they can easily discover the work and discussions.
</div>

Here's an example of adding an "Unreleased" section to a `CHANGELOG.md` with a `0.1.0` section already released.

    [lang=markdown]
    ## [Unreleased]

    ### Added
    - Does cool stuff! (https://github.com/MyGithubUsername/MyCoolNewLib/pull/001)

    ### Fixed
    - Fixes that silly oversight (https://github.com/MyGithubUsername/MyCoolNewLib/pull/002)

    ## [0.1.0] - 2017-03-17
    First release

    ### Added
    - This release already has lots of features

    [Unreleased]: https://github.com/user/MyCoolNewLib.git/compare/v0.1.0...HEAD
    [0.1.0]: https://github.com/user/MyCoolNewLib.git/releases/tag/v0.1.0

- You can then use the `Release` target, specifying the version number either in the `RELEASE_VERSION` environment
  variable, or else as a parameter after the target name.  This will:
  - update `CHANGELOG.md`, moving changes from the `Unreleased` section into a new `0.2.0` section
    - if there were any prerelease versions of 0.2.0 in the changelog, it will also collect their changes into the final 0.2.0 entry
  - make a commit bumping the version:  `Bump version to 0.2.0` and adds the new changelog section to the commit's body
  - publish the package to NuGet
  - push a git tag
  - create a GitHub release for that git tag

macOS/Linux Parameter:

    [lang=bash]
    ./build.sh Release 0.2.0

macOS/Linux Environment Variable:

    [lang=bash]
    RELEASE_VERSION=0.2.0 ./build.sh Release

## Done

You have now successfully created your first OSS library that has been published to NuGet and GitHub. Congratulations!
