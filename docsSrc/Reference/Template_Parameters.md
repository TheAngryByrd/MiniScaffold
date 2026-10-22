---
title: MiniScaffold template parameters
category: Reference
categoryindex: 4
index: 10
---

# MiniScaffold template parameters

The template short name is `mini-scaffold`.
The configuration in `Content/.template.config/template.json` defines the template parameters.

## `--githubUsername`

This parameter replaces `MyGithubUsername` with a GitHub username or organization name.
Generated repository URLs and package metadata use this value.
The parameter has no default value.

Example:

    [lang=bash]
    dotnet new mini-scaffold -n MyCoolNewLib --githubUsername MyGithubUsername

## `--outputType`

This choice parameter selects the generated content.
The default value is `library`.

| Value | Generated content |
| --- | --- |
| `library` | A complete library solution. |
| `console` | A complete console solution. |
| `projLib` | One library project for an existing solution. |
| `projConsole` | One console project for an existing solution. |
| `projTest` | One test project for an existing solution. |

Example:

    [lang=bash]
    dotnet new mini-scaffold -n MyCoolNewApp --githubUsername MyGithubUsername --outputType console

## `--releaseBranch`

This parameter replaces `MyReleaseBranch` with the release branch name.
The default value is `main`.
Generated build scripts, badges, documentation links, and workflows use this value.
The parameter does not change Git branches or GitHub repository settings.

Example:

    [lang=bash]
    dotnet new mini-scaffold -n MyCoolNewLib --githubUsername MyGithubUsername --releaseBranch master

## Standard `dotnet new` options

The `--name` option, or `-n`, specifies the generated name that replaces `MyLib.1`.
The `--output` option, or `-o`, specifies the output directory.
Without `--output`, MiniScaffold uses a directory with the generated name.
