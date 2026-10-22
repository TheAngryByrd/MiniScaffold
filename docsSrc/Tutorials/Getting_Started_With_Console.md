---
title: Get started with a console solution
category: Tutorials
categoryindex: 0
index: 200
---

# Get started with a console solution

In this tutorial, you create a console application, run its tests, and create packages for Windows, macOS, and Linux.

## Prerequisites

1. Install [Git](https://git-scm.com/download).
2. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
3. Install the [.NET 8 runtime](https://dotnet.microsoft.com/download/dotnet/8.0) for the build project.
4. Open a terminal in the directory that will contain your new repository.

## Install MiniScaffold

Run this command to install the template from NuGet:

    [lang=bash]
    dotnet new install "MiniScaffold::*"

The command installs the `mini-scaffold` template.

## Create the console solution

Replace `MyGithubUsername` with your GitHub username.
Run this command:

    [lang=bash]
    dotnet new mini-scaffold -n MyCoolNewApp --githubUsername MyGithubUsername --outputType console

The command creates a directory named `MyCoolNewApp`.
Open that directory:

    [lang=bash]
    cd MyCoolNewApp

The generated solution contains these files:

- `MyCoolNewApp.slnx`, the solution file.
- `src/MyCoolNewApp/MyCoolNewApp.fsproj`, the application project.
- `tests/MyCoolNewApp.Tests/MyCoolNewApp.Tests.fsproj`, the test project.
- `build/`, the directory with the FAKE build scripts.
- `build.cmd` and `build.sh`, the build commands.

## Build, test, and create packages

On macOS or Linux, run this command:

    [lang=bash]
    ./build.sh

On Windows, run this command:

    [lang=powershell]
    .\build.cmd

The default target, `CreatePackages`, restores dependencies, builds the solution, runs the tests, and creates packages.
The build output reports the result of each target.

After the build succeeds, check `dist/` for the Windows ZIP package and the macOS and Linux tarballs.
The [console output reference](../Reference/Console_Output.md) describes the generated files and package formats.
