---
title: Get Started with a Library solution
category: Tutorials
categoryindex: 0
index: 100
---

# Getting Started

In this tutorial, we'll take a look at getting started with MiniScaffold and publishing your first library to NuGet and GitHub.

## Prerequisites

- Install [git](https://git-scm.com/download)
- Install [.Net core](https://dotnet.microsoft.com/download)
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
    ├── CHANGELOG.md
    ├── Directory.Build.props
    ├── Directory.Build.targets
    ├── LICENSE.md
    ├── README.md
    ├── build
    │   ├── Changelog.fs
    │   ├── FsDocs.fs
    │   ├── build.fs
    │   ├── build.fsproj
    │   └── paket.references
    ├── build.cmd
    ├── build.sh
    ├── docsSrc
    │   ├── Explanations
    │   │   └── Background.md
    │   ├── How_Tos
    │   │   ├── Doing_A_Thing.md
    │   │   └── Doing_Another_Thing.md
    │   ├── Tutorials
    │   │   └── Getting_Started.md
    │   ├── _menu-item_template.html
    │   ├── _menu_template.html
    │   ├── _template.html
    │   ├── content
    │   │   ├── fsdocs-custom.css
    │   │   ├── fsdocs-dark.css
    │   │   ├── fsdocs-light.css
    │   │   ├── fsdocs-main.css
    │   │   ├── navbar-fixed-left.css
    │   │   └── theme-toggle.js
    │   └── index.md
    ├── global.json
    ├── MyCoolNewLib.sln
    ├── paket.dependencies
    ├── paket.lock
    ├── src
    │   ├── Directory.Build.props
    │   └── MyCoolNewLib
    │       ├── AssemblyInfo.fs
    │       ├── Library.fs
    │       ├── MyCoolNewLib.fsproj
    │       └── paket.references
    └── tests
        ├── Directory.Build.props
        └── MyCoolNewLib.Tests
            ├── Main.fs
            ├── Tests.fs
            ├── MyCoolNewLib.Tests.fsproj
            └── paket.references

This may look overwhelming, but we don't have to worry about all of these yet.  Let's just focus on the real important ones for this tutorial.

- `./src/MyCoolNewLib` - This is where your library's source code will live.
- `./tests/MyCoolNewLib.Tests` - This is where your library's test code will live.
- `.\build.cmd` or `./build.sh` - Platform specific entry points into your `build` project.
- `./build/` - The main build script of your repository.
- `./README.md` - The text file that introduces and explains a project.
- `./CHANGELOG.md` - Text file containing versioning, date, and release notes.

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

The README.md comes with a lot of information but it's recommended to fill out the introduction description.

## Making a Release

The release process is streamlined so you only have to start your git repository, set your NuGet keys for GitHub Actions, create `CHANGELOG` notes, and run the `Release` build target. After you've done the initial setup, you will only need to perform the last two steps for each release.

### Create your Github Repository

- [Create a GitHub Repository](https://help.github.com/en/github/getting-started-with-github/create-a-repo) and
[Start a git repo with your GitHub repository as a remote](https://help.github.com/articles/adding-an-existing-project-to-github-using-the-command-line/)

    [lang=bash]
    git init
    git add .
    git commit -m "Scaffold"
    git remote add origin https://github.com/MyGithubUsername/MyCoolNewLib.git
    git push -u origin master
    ```

### Add your NUGET_TOKEN to your environment

- Our NuGet package is created with a [GitHub Action](https://github.com/features/actions)  This action needs to be able to push to NuGet.  To do this, we need to add our NuGet API key to our GitHub repository's secrets. 
- [Create an Environment](https://docs.github.com/en/actions/deployment/targeting-different-environments/using-environments-for-deployment#creating-an-environment) on your repository named `nuget`.
- [Create a NuGet API key](https://learn.microsoft.com/en-us/nuget/nuget-org/publish-a-package#create-an-api-key)
- Add your `NUGET_TOKEN` to the [Environment Secrets](https://docs.github.com/en/actions/deployment/targeting-different-environments/using-environments-for-deployment#environment-secrets) of your newly created environment.


### Prepare your CHANGELOG.md

Then update the `CHANGELOG.md` with an "Unreleased" section containing release notes for this version, in [KeepAChangelog](https://keepachangelog.com/en/1.1.0/) format.

<div class="alert alert-primary" role="alert">
    <p>
        NOTE: Its highly recommend to add a link to the Pull Request next to the release note that it affects. The reason for this is when the <code>RELEASE</code> target is run, it will add these new notes into the body of git commit. GitHub will notice the links and will update the Pull Request with what commit referenced it saying <a href="https://github.com/TheAngryByrd/MiniScaffold/pull/179#ref-commit-837ad59">"added a commit that referenced this pull request"</a>. Since we automate the commit message, it will say "Bump Version to x.y.z". The benefit of this is when users goto a Pull Request, it will be clear when and which version those code changes released. Also when reading the <code>CHANGELOG</code>, if someone is curious about how or why those changes were made, they can easily discover the work and discussions.
    </p>
    <p>
        Additionally adding the GitHub handle of the user that contributed the pull request will allow GitHub to notify them of the release and have them show up as contributors for that release.
    </p>
</div>

Here's an example of adding an `Unreleased` section to a `CHANGELOG.md` with a `0.1.0` section already released.


    [lang=markdown]
    ## [Unreleased]

    ### Added
    - Does cool stuff! (https://github.com/MyGithubUsername/MyCoolNewLib/pull/001) (Thanks @TheAngryByrd!)

    ### Fixed
    - Fixes that silly oversight (https://github.com/MyGithubUsername/MyCoolNewLib/pull/002) (Thanks! @baronfel)

    ## [0.1.0] - 2017-03-17
    First release

    ### Added
    - This release already has lots of features

    [Unreleased]: https://github.com/user/MyCoolNewLib.git/compare/v0.1.0...HEAD
    [0.1.0]: https://github.com/user/MyCoolNewLib.git/releases/tag/v0.1.0

- You can then use the `Release` target, specifying the version number either in the `RELEASE_VERSION` environment
  variable, or else as a parameter after the target name. 
    - `./build.sh Release 0.2.0`

- This target will do the following for you:
  - update `CHANGELOG.md`, moving changes from the `Unreleased` section into a new `0.2.0` section
    - if there were any prerelease versions of 0.2.0 in the changelog, it will also collect their changes into the final 0.2.0 entry
  - make a commit bumping the version:  `Bump version to 0.2.0` and adds the new changelog section to the commit's body
  - push a git tag
- The GitHub Action will then:
  - publish the package to NuGet
  - create a GitHub release for that git tag

macOS/Linux Parameter:

    [lang=bash]
    ./build.sh Release 0.2.0

macOS/Linux Environment Variable:

    [lang=bash]
    RELEASE_VERSION=0.2.0 ./build.sh Release

## Done

You have now successfully created your first OSS library that has been published to NuGet and GitHub. Congratulations!
