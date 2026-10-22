---
title: Use a custom release branch
category: How To Guides
categoryindex: 1
index: 500
---

# Use a custom release branch

MiniScaffold uses `main` as the default release branch in generated repositories.
The `--releaseBranch` parameter changes this value during generation.
The parameter does not create a Git branch or change the default branch on GitHub.

## Generate the repository

1. Choose the branch name that your repository uses for releases.
2. Replace `master` in this command with that branch name.
3. Replace `MyCoolNewLib` and `MyGithubUsername` with your library name and GitHub username.
4. Run the command:

       [lang=bash]
       dotnet new mini-scaffold -n MyCoolNewLib --githubUsername MyGithubUsername --outputType library --releaseBranch master

5. Create and publish the Git repository on the selected branch.
6. Set that branch as the default branch in the GitHub repository settings.

## Change the branch after generation

1. Update `releaseBranch` in `build/build.fs`.
2. Update branch names in the generated workflows under `.github/workflows/`.
3. Update branch names in README badges and documentation links.
4. Set the new default branch in the GitHub repository settings.
5. Before a release, switch to the configured release branch.

The build checks the current branch before it creates a release.
