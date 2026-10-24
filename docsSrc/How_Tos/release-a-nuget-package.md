---
title: Release a NuGet package
category: How To Guides
categoryindex: 1
index: 600
---

# Release a NuGet package

Use this guide to publish a package from a generated MiniScaffold library repository.
MiniScaffold maintainers use the same trusted publishing setup for the root template package.
The local `Release` target creates a release commit and pushes the commit and a Git tag.
The tag starts a GitHub Actions workflow that publishes the package.

## Prepare the repository

1. Generate a repository with `--outputType library`.
2. Install the .NET SDK and runtimes that the generated projects require.
3. Publish the repository on GitHub.
4. Configure the Git remote `origin` for that GitHub repository.
5. Use an account with permission to push commits and tags.
6. Obtain administrator access to configure the GitHub repository environments.

## Configure trusted publishing

1. In the GitHub repository, open **Settings**.
2. Select **Environments**.
3. Select the `nuget` environment, or create it if it does not exist.
4. Open [NuGet trusted publishing](https://www.nuget.org/account/trustedpublishing).
5. Select the correct NuGet package owner and package scopes for a new policy.
6. Set the policy fields to the repository owner, repository name, workflow filename `publish.yml`, and environment `nuget`.
7. Use the actual repository name if you renamed the generated repository.
8. In the GitHub `nuget` environment, add an environment variable named `NUGET_USER`.
9. Set the variable to the policy creator's actual NuGet profile name.

Use the workflow filename, not the workflow display name or `.github/workflows/` path.
Do not use an email address or infer the NuGet profile from the GitHub owner.
The NuGet package owner can be an organization with a different name.
The root MiniScaffold policy uses GitHub owner `TheAngryByrd` and repository `MiniScaffold`.
Generated repositories use their own GitHub owner and repository name.

Confirm the policy and profile configuration before you authorize a release.
Keep any existing GitHub `NUGET_TOKEN` secret and scoped NuGet key until the publication checks below pass.
The workflow does not use those stored credentials as a fallback.

The publishing job grants `id-token: write` for OIDC and `contents: write` for the GitHub release.
`NuGet/login@v1` exchanges the job identity for a short-lived NuGet API key.
The workflow maps its `NUGET_API_KEY` output to the existing `NUGET_TOKEN` build input.

## Prepare the release

1. Switch to the configured release branch.
2. Add the release notes under `Unreleased` in `CHANGELOG.md`.
3. Use the Keep a Changelog format.
4. Commit all changes that you want to include in the release.
5. Check that the working tree has no uncommitted changes.
6. Check that the build and tests pass before you run the release target.
7. Run the `DotnetPack` target and inspect the packages in `dist/` before you authorize publication.

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
4. Check that the GitHub release contains the expected release notes and package artifacts.
5. Check the complete run logs for credentials and credential fragments.
6. Check the actual NuGet policy status and confirm that it is active and permanent.

Record the check results without copying credential values or fragments.
Do not infer permanent activation from repository visibility or a successful workflow alone.

### Retire the old key

Only after all publication verification succeeds, remove the stored GitHub `NUGET_TOKEN` secret and revoke the old scoped NuGet key.
The workflow change does not remove either credential.

The workflow builds the package in the Release configuration.
The `PublishToNuGet` target uses `NUGET_TOKEN` to publish the package.
The `Publish` target also creates a GitHub release.

## Resolve release failures

If the build reports that you are on the wrong branch, switch to the configured release branch.
If publication fails, check the workflow output before you run another release.
Use these checks before you authorize another publication:

| Failure | Check |
| --- | --- |
| Login cannot request an OIDC token | Check `id-token: write` on the publishing job. |
| No matching policy | Check the GitHub owner, repository, workflow filename `publish.yml`, and environment `nuget`. |
| Profile or ownership failure | Check `vars.NUGET_USER`, the policy creator's NuGet profile, the package owner, and package scopes. |
| Expired policy | Check the actual policy status and its activation deadline on NuGet. |
| Expired API key | Check the time from login to push. The short-lived key expires after one hour. |
| Package version or GitHub release already exists | Inspect both publication destinations before you choose a recovery action. |

The login step immediately precedes `Publish`, but that target builds and checks the package before the push.
If this work exceeds the token lifetime, review the workflow design before another publication.
Do not add a stored-key fallback, move an existing tag, or assume that a rerun uses newer workflow code.
For policy details, see [NuGet trusted publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing).

## Recover manually with a temporary key

Use this procedure only after a maintainer authorizes emergency publication.

1. Inspect NuGet and GitHub to identify any completed publication steps.
2. Review the exact package artifacts that still need publication.
3. Create a new temporary NuGet API key with the shortest practical expiry.
4. Restrict the key to the exact package IDs or package glob and only the required push permission.
5. Use `dotnet nuget push` to publish the reviewed artifacts with private credential input.
6. Revoke the temporary key after recovery.
7. If the GitHub release is incomplete, review its current state before you repair it separately.

Keep the key out of command examples, command history, logs, and workflow configuration.
Do not restore automatic key fallback or move the release tag.
