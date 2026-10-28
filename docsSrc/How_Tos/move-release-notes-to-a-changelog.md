---
title: Move release notes to a changelog
category: How To Guides
categoryindex: 1
index: 700
---

# Move release notes to a changelog

Repositories from MiniScaffold versions before 0.23.0 keep the release history in `RELEASE_NOTES.md`.
Later versions keep the release history in `CHANGELOG.md`.
The `tools/releaseToChangelog.fsx` script converts `RELEASE_NOTES.md` to `CHANGELOG.md`.
The result uses the [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) format.

## Prepare the conversion

1. Install the .NET SDK 8.0.400 or later.
2. Commit all changes in your repository.
3. If a `CHANGELOG.md` file is in the same directory as `RELEASE_NOTES.md`, move or delete that file.

The script does not replace an existing `CHANGELOG.md` file.

## Download the script

On macOS or Linux, run this command from the repository root:

    [lang=bash]
    curl -L -o releaseToChangelog.fsx https://raw.githubusercontent.com/TheAngryByrd/MiniScaffold/master/tools/releaseToChangelog.fsx

On Windows, run this command from the repository root:

    [lang=powershell]
    Invoke-WebRequest -Uri https://raw.githubusercontent.com/TheAngryByrd/MiniScaffold/master/tools/releaseToChangelog.fsx -OutFile releaseToChangelog.fsx

## Run the conversion

1. Replace `MyGithubUsername` and `MyCoolNewLib` in the next command with your GitHub owner and repository name.
2. Run the command from the repository root:

       [lang=bash]
       dotnet fsi releaseToChangelog.fsx MyGithubUsername MyCoolNewLib RELEASE_NOTES.md

The script writes `CHANGELOG.md` in the directory of `RELEASE_NOTES.md`.
The script sorts the versions from newest to oldest.
The first run downloads the FAKE packages from NuGet.
If an argument is missing, the script shows the usage and stops.
If the script cannot find a version in `RELEASE_NOTES.md`, the script stops and does not write `CHANGELOG.md`.

## Check the result

The script puts each note in a section. The note title selects the section:

| Note title in `RELEASE_NOTES.md` | Section in `CHANGELOG.md` |
| --- | --- |
| `FEATURE:` | `Added` |
| `BUGFIX:` | `Fixed` |
| `MINOR:` or `MAINTENANCE:` | `Changed` |
| A different title, such as `DOCUMENTATION:` | A section with that title, such as `DOCUMENTATION` |
| No title | `Changed` |

The script puts an indented note at the same level as the note above it.

1. Open `CHANGELOG.md`.
2. Check the notes in each version section.
3. If a section has a different title, move its notes to a [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) section.
4. Check the link references at the end of the file.

The link references use Git tags such as `v0.2.0`.
The MiniScaffold release target creates tags in this format.
If your Git tags do not start with `v`, change the link references.

## Complete the move

1. Delete `releaseToChangelog.fsx`.
2. Delete `RELEASE_NOTES.md`.
3. If your build script reads `RELEASE_NOTES.md`, change the build script to read `CHANGELOG.md`.
4. Use `build/Changelog.fs` in a new generated repository as an example.
5. Commit `CHANGELOG.md` and the other changes.

For the next release, add the release notes under `## [Unreleased]` in `CHANGELOG.md`.
For the release procedure, use the [NuGet release guide](release-a-nuget-package.md).
