---
title: How MiniScaffold works
category: Explanations
categoryindex: 3
index: 200
---

# How MiniScaffold works

MiniScaffold is a `dotnet new` template.
The template engine copies files from the package and replaces placeholders with parameter values.
The generated files include the source code, build scripts, and configuration for the selected output type.

## Template parameters

The template configuration in `Content/.template.config/template.json` defines these substitutions:

- The solution name replaces `MyLib.1`, the template's `sourceName`.
- `--githubUsername` replaces `MyGithubUsername` in repository links and package metadata.
- `--releaseBranch` replaces `MyReleaseBranch` in build scripts, documentation links, and workflow configuration.

The [template parameter reference](../Reference/Template_Parameters.md) describes the available values.

## Output types

The `--outputType` parameter selects a source directory under `Content/`.
The `library` and `console` values generate complete repositories.
The `projLib`, `projConsole`, and `projTest` values generate individual projects for an existing solution.

Separate source directories let each output type contain its own files and configuration.
The template engine selects the directory before it copies the files.

## Generated repositories

The library and console outputs include build scripts, tests, and GitHub Actions workflows.
The library output also includes documentation source and build targets.
These files give each new repository a build process that developers can change with the application or library.

MiniScaffold keeps these files under `Content/` so maintainers can edit the template content directly.
The template engine handles file selection and text replacement.
