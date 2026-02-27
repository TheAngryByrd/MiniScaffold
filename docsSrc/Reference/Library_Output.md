# Library Scaffold Output

When you do an initial scaffold, your folder will be filled with this set of files.  We'll go one by one and talk about what they're and additional references for them.

    [lang=bash]
    .
    ├── .config
    │   └── dotnet-tools.json
    ├── .devcontainer
    │   ├── Dockerfile
    │   └── devcontainer.json
    ├── .editorconfig
    ├── .fantomasignore
    ├── .git-blame-ignore-revs
    ├── .gitattributes
    ├── .github
    │   ├── ISSUE_TEMPLATE
    │   │   ├── bug_report.md
    │   │   └── feature_request.md
    │   ├── ISSUE_TEMPLATE.md
    │   ├── PULL_REQUEST_TEMPLATE.md
    │   └── workflows
    │       ├── build.yml
    │       ├── fsdocs-gh-pages.yml
    │       └── publish.yml
    ├── .gitignore
    ├── .vscode
    │   ├── extensions.json
    │   └── settings.json
    ├── CHANGELOG.md
    ├── Directory.Build.props
    ├── Directory.Build.targets
    ├── Directory.Packages.props
    ├── LICENSE.md
    ├── MyLib.1.slnx
    ├── NuGet.config
    ├── README.md
    ├── build
    │   ├── Changelog.fs
    │   ├── FsDocs.fs
    │   ├── build.fs
    │   └── build.fsproj
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
    ├── src
    │   ├── Directory.Build.props
    │   ├── Directory.Build.targets
    │   └── MyLib.1
    │       ├── AssemblyInfo.fs
    │       ├── Library.fs
    │       └── MyLib.1.fsproj
    └── tests
        ├── Directory.Build.props
        └── MyLib.1.Tests
            ├── Main.fs
            ├── MyLib.1.Tests.fsproj
            └── Tests.fs

- `.config\dotnet-tools.json` - Holds [dotnet tools](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-tool-install) manifest.
- `.devcontainer\` - Holds all files necessary for [VSCode Dev Container](https://code.visualstudio.com/docs/remote/containers) feature.
- `.editorconfig` - [EditorConfig](https://editorconfig.org/).
- `.fantomasignore` - Configuration file for [Fantomas](https://fsprojects.github.io/fantomas/) F# code formatter.
- `.git-blame-ignore-revs` - List of commits to ignore in `git blame` output, typically used for bulk formatting changes.
- `.gitattributes` - Git attributes file for controlling line endings and diff behavior.
- `.github\` - Holds all [GitHub] related templates.
- `.gitignore` - Good set of defaults for dotnet related repositories.
- `.vscode\extensions.json` - File containing all recommended VSCode plugins for this repository.
- `.vscode\settings.json` - File containing all VSCode settings for this repository.
- `Directory.Build.props` - Top level configuration for project files. See [Microsoft Docs](https://docs.microsoft.com/en-us/visualstudio/msbuild/customize-your-build?view=vs-2019#directorybuildprops-and-directorybuildtargets) for more info.
- `Directory.Build.targets` - Top level targets for project files. See [Microsoft Docs](https://docs.microsoft.com/en-us/visualstudio/msbuild/customize-your-build?view=vs-2019#directorybuildprops-and-directorybuildtargets) for more info.
- `Directory.Packages.props` - Central package version management for NuGet packages. See [Microsoft Docs](https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management) for more info.
- `LICENSE.md` - Your repositories license. Starts with MIT. [Choose a license](https://choosealicense.com/) if you're looking for more choices.
- `MyCoolNewLib.slnx` - Solution file for your repository. See [Microsoft Docs](https://devblogs.microsoft.com/dotnet/introducing-slnx-support-dotnet-cli/) for more info.
- `NuGet.config` - NuGet configuration file for package sources.
- `README.md` - The text file that introduces and explains a project.
- `CHANGELOG.md` - The text file containing versioning, date, and release notes.
- `build.cmd` - Windows specific entry point for building the repository.
- `build.sh` - Nix specific entry point for building the repository.
- `build\` - Build script for building the repository. See [FAKE Docs](https://fake.build/) for more info.
- `docsSrc\` - Contains the source files for your [GitHub documentation](https://help.github.com/en/github/working-with-github-pages/configuring-a-publishing-source-for-your-github-pages-site).
- `src\` - Folder containing your repository's [project files](https://docs.microsoft.com/en-us/aspnet/web-forms/overview/deployment/web-deployment-in-the-enterprise/understanding-the-project-file) and code.
- `tests\` - Folder containing tests running against your code in `src`.
