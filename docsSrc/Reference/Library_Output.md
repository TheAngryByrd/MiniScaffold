# Library Scaffold Output

When you do an initial scaffold, your folder will be filled with this set of files.  We'll go one by one and talk about what they're and additional references for them.

    [lang=bash]
    .
    |-- .config
    |   |-- dotnet-tools.json
    |-- .devcontainer
    |   |-- Dockerfile
    |   |-- devcontainer.json
    |   |-- settings.vscode.json
    |-- .editorconfig
    |-- .gitattributes
    |-- .github
    |   |-- ISSUE_TEMPLATE
    |   |   |-- bug_report.md
    |   |   |-- feature_request.md
    |   |-- ISSUE_TEMPLATE.md
    |   |-- PULL_REQUEST_TEMPLATE.md
    |   |-- build.yml
    |   |-- workflows
    |       |-- build.yml
    |-- .gitignore
    |-- .paket
    |   |-- Paket.Restore.targets
    |-- .travis.yml
    |-- .vscode
    |   |-- extensions.json
    |   |-- settings.json
    |-- Directory.Build.props
    |-- LICENSE.md
    |-- MyCoolNewLib.sln
    |-- README.md
    |-- CHANGELOG.md
    |-- appveyor.yml
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

- `.config\dotnet-tools.json` - Holds [dotnet tools](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-tool-install) manifest.
- `.devcontainer\` - Holds all files necessary for [VSCode Dev Container](https://code.visualstudio.com/docs/remote/containers) feature.
- `.editorconfig` - [EditorConfig](https://editorconfig.org/).
- `.github\` - Holds all [GitHub] related templates.
- `.gitignore` - Good set of defaults for dotnet related repositories.
- `.paket\Paket.Restore.targets` - Needed for paket to interact with MSBuild. See [paket](https://fsprojects.github.io/Paket/).
- `.travis.yml` - File containing default CI setup for [TravisCI](https://travis-ci.org/). Used for Linux/macOS builds.
- `.vscode\extensions.json` - File containing all recommended VSCode plugins for this repository.
- `.vscode\settings.json` - File containing all VSCode settings for this repository.
- `Directory.Build.props` - Top level configuration for project files. See [Microsoft Docs](https://docs.microsoft.com/en-us/visualstudio/msbuild/customize-your-build?view=vs-2019#directorybuildprops-and-directorybuildtargets) for more info.
- `LICENSE.md` - Your repositories license. Starts with MIT. [Choose a license](https://choosealicense.com/) if you're looking for more choices.
- `MyCoolNewLib.sln` - Solution file for your repository. See [Microsoft Docs](https://docs.microsoft.com/en-us/visualstudio/extensibility/internals/solution-dot-sln-file?view=vs-2019) for more info.
- `README.md` - The text file that introduces and explains a project.
- `CHANGELOG.md` - The text file containing versioning, date, and release notes.
- `appveyor.yml` File contained default CI setup for [AppVeyor](https://www.appveyor.com/). Used for Windows builds.
- `build.cmd` - Windows specific entry point for building the repository.
- `build.sh` - Nix specific entry point for building the repository.
- `build.fsx` - Build script for building the repository. See [FAKE Docs](https://fake.build/) for more info.
- `docsSrc\` - Contains the source files for your [GitHub documentation](https://help.github.com/en/github/working-with-github-pages/configuring-a-publishing-source-for-your-github-pages-site).
- `docsTool\` - Contains the tool for generating your [GitHub documentation](https://help.github.com/en/github/working-with-github-pages/configuring-a-publishing-source-for-your-github-pages-site).
- `paket.dependencies` - File containing your repositories dependencies.  See [Paket Docs](https://fsprojects.github.io/Paket/dependencies-file.html) for more info.
- `paket.lock` - File containing the full dependency graph of your repository.  See [Paket Docs](https://fsprojects.github.io/Paket/lock-file.html) for more info.
- `src\` - Folder containing your repository's [project files](https://docs.microsoft.com/en-us/aspnet/web-forms/overview/deployment/web-deployment-in-the-enterprise/understanding-the-project-file) and code.
- `tests\` - Folder containing tests running against your code in `src`.
