---
title: Release a NuGet package
category: How To Guides
categoryindex: 1
index: 600
---

# Release a NuGet package

Use this guide to publish a package from a generated MiniScaffold library repository.
The local `Release` target creates a release commit and pushes the commit and a Git tag.
The tag starts a GitHub Actions workflow that publishes the package.

## Prepare the repository

1. Generate a repository with `--outputType library`.
2. Install the .NET SDK and runtimes that the generated projects require.
3. Publish the repository on GitHub.
4. Configure the Git remote `origin` for that GitHub repository.
5. Use an account with permission to push commits and tags.
6. Obtain administrator access to configure the GitHub repository environments.

## Configure the NuGet secret

1. In the GitHub repository, open **Settings**.
2. Select **Environments**.
3. Select **New environment**.
4. Create an environment named `nuget`.
5. Create a [NuGet API key](https://learn.microsoft.com/en-us/nuget/nuget-org/publish-a-package#create-an-api-key) with permission to publish your package.
6. In the `nuget` environment, open **Environment secrets**.
7. Add a secret named `NUGET_TOKEN` with the API key as its value.

## Prepare the release

1. Switch to the configured release branch.
2. Add the release notes under `Unreleased` in `CHANGELOG.md`.
3. Use the Keep a Changelog format.
4. Commit all changes that you want to include in the release.
5. Check that the working tree has no uncommitted changes.
6. Check that the build and tests pass before you run the release target.

For a custom branch, use the [release branch guide](use-a-custom-release-branch.md).

The build uses `CHANGELOG.md` for the package version and release notes.
The release commit body also contains the release notes.
A pull request URL in those notes lets GitHub link the release commit to that pull request.

Example entry:

    - Add a feature (https://github.com/<org>/<repo>/pull/123)

## Create the release

Replace `0.2.0` with the new version number.
Use a version without a leading `v`.

On macOS or Linux, run this command from the repository root:

    [lang=bash]
    ./build.sh Release 0.2.0

On Windows, run this command from the repository root:

    [lang=powershell]
    .\build.cmd Release 0.2.0

The target moves the `Unreleased` notes into a version section in `CHANGELOG.md`.
The target creates a commit with a message such as `Bump version to 0.2.0`.
The target pushes the commit, creates a tag such as `v0.2.0`, and pushes the tag to `origin`.

## Check publication

1. In GitHub Actions, open the run for `.github/workflows/publish.yml` that the tag started.
2. Check that the `Publish` target succeeds.
3. Check that nuget.org lists the new package version.

The workflow builds the package in the Release configuration.
The `PublishToNuGet` target uses `NUGET_TOKEN` to publish the package.
The `Publish` target also creates a GitHub release.

## Resolve release failures

If the build reports that you are on the wrong branch, switch to the configured release branch.
If publication fails, check the workflow output before you run another release.
Check that the `nuget` environment contains `NUGET_TOKEN` with permission to publish the package.
