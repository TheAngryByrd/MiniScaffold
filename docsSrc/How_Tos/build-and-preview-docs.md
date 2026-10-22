---
title: Build and preview the documentation locally
category: How To Guides
categoryindex: 1
index: 400
---

# Build and preview the documentation locally

Use this guide for the MiniScaffold repository or a generated library repository.
The `BuildDocs` and `WatchDocs` targets use `fsdocs` to generate a site from `docsSrc/`.

## Prepare the repository

1. Install the .NET SDK that the repository's `global.json` specifies.
2. Install the .NET 8 runtime for the build project.
3. Open a terminal in the repository root.
4. Add each new page to the applicable `docsSrc/` directory.
5. Add a link to the page in that directory's `0-toc.md` file.

## Build the documentation

On macOS or Linux, run this command:

    [lang=bash]
    ./build.sh BuildDocs

On Windows, run this command:

    [lang=powershell]
    .\build.cmd BuildDocs

The target generates the site in `docs/`.

## Preview the documentation

On macOS or Linux, run this command:

    [lang=bash]
    ./build.sh WatchDocs

On Windows, run this command:

    [lang=powershell]
    .\build.cmd WatchDocs

The target generates the site in `temp/watch-docs/` and starts a local web server.
The server updates the preview when you change the documentation source.

If the browser does not open, open the URL from the terminal output in a browser.
Check each new page and its navigation links.
To stop the server, press `Ctrl+C` in the terminal.
