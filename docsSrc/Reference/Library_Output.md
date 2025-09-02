# Library Scaffold Output

When you do an initial scaffold, your folder will be filled with this set of files.  We'll go one by one and talk about what they're and additional references for them.

    [lang=bash]
    .
    ├── CHANGELOG.md
    ├── Directory.Build.props
    ├── LICENSE.md
    ├── MyLib.1.sln
    ├── README.md
    ├── build
    │   ├── build.fs
    │   ├── build.fsproj
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
    │   ├── content
    │   │   ├── cleanups.js
    │   │   ├── hotload.js
    │   │   ├── style.css
    │   │   ├── submenu.js
    │   │   ├── themes.js
    │   │   ├── tips.js
    │   │   ├── toggle-bootstrap-dark.min.css
    │   │   └── toggle-bootstrap.min.css
    │   ├── files
    │   │   └── placeholder.md
    │   └── index.md
    ├── docsTool
    │   ├── CLI.fs
    │   ├── Prelude.fs
    │   ├── Program.fs
    │   ├── README.md
    │   ├── WebServer.fs
    │   ├── docsTool.fsproj
    │   └── templates
    │       ├── helpers.fs
    │       ├── master.fs
    │       ├── modules.fs
    │       ├── namespaces.fs
    │       ├── nav.fs
    │       ├── partMembers.fs
    │       ├── partNested.fs
    │       └── types.fs
    ├── global.json
    ├── src
    │   ├── Directory.Build.props
    │   └── MyLib.1
    │       ├── AssemblyInfo.fs
    │       ├── Library.fs
    │       ├── MyLib.1.fsproj
    └── tests
        ├── Directory.Build.props
        └── MyLib.1.Tests
            ├── AssemblyInfo.fs
            ├── Main.fs
            ├── MyLib.1.Tests.fsproj
            ├── Tests.fs

- `.config\dotnet-tools.json` - Holds [dotnet tools](https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-tool-install) manifest.
- `.devcontainer\` - Holds all files necessary for [VSCode Dev Container](https://code.visualstudio.com/docs/remote/containers) feature.
- `.editorconfig` - [EditorConfig](https://editorconfig.org/).
- `.github\` - Holds all [GitHub] related templates.
- `.gitignore` - Good set of defaults for dotnet related repositories.
- `.vscode\extensions.json` - File containing all recommended VSCode plugins for this repository.
- `.vscode\settings.json` - File containing all VSCode settings for this repository.
- `Directory.Build.props` - Top level configuration for project files. See [Microsoft Docs](https://docs.microsoft.com/en-us/visualstudio/msbuild/customize-your-build?view=vs-2019#directorybuildprops-and-directorybuildtargets) for more info.
- `LICENSE.md` - Your repositories license. Starts with MIT. [Choose a license](https://choosealicense.com/) if you're looking for more choices.
- `MyCoolNewLib.sln` - Solution file for your repository. See [Microsoft Docs](https://docs.microsoft.com/en-us/visualstudio/extensibility/internals/solution-dot-sln-file?view=vs-2019) for more info.
- `README.md` - The text file that introduces and explains a project.
- `CHANGELOG.md` - The text file containing versioning, date, and release notes.
- `build.cmd` - Windows specific entry point for building the repository.
- `build.sh` - Nix specific entry point for building the repository.
- `build\` - Build script for building the repository. See [FAKE Docs](https://fake.build/) for more info.
- `docsSrc\` - Contains the source files for your [GitHub documentation](https://help.github.com/en/github/working-with-github-pages/configuring-a-publishing-source-for-your-github-pages-site).
- `docsTool\` - Contains the tool for generating your [GitHub documentation](https://help.github.com/en/github/working-with-github-pages/configuring-a-publishing-source-for-your-github-pages-site).
- `src\` - Folder containing your repository's [project files](https://docs.microsoft.com/en-us/aspnet/web-forms/overview/deployment/web-deployment-in-the-enterprise/understanding-the-project-file) and code.
- `tests\` - Folder containing tests running against your code in `src`.
