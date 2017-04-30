# MiniScaffold
F# Template for creating and publishing libraries targeting .NET Full (net45) and Core (netstandard1.6)

[![NuGet Badge](https://img.shields.io/nuget/vpre/MiniScaffold.svg)](https://www.nuget.org/packages/MiniScaffold/)

[![Travis Badge](https://travis-ci.org/TheAngryByrd/MiniScaffold.svg?branch=master)](https://travis-ci.org/TheAngryByrd/MiniScaffold)


## Getting started

### Grab the template from nuget:

```
dotnet new -i MiniScaffold::*
```

### Use the new template:

```
dotnet new mini-scaffold -n MyCoolNewLib
cd MyCoolNewLib
```

It will scaffold out something similar to:

```
$ tree
.
├── LICENSE.md
├── README.md
├── RELEASE_NOTES.md
├── build.cmd
├── build.fsx
├── build.sh
├── paket.dependencies
├── paket.lock
├── src
│   └── MyCoolNewLib
│       ├── Library.fs
│       └── MyCoolNewLib.fsproj
└── tests
    └── MyCoolNewLib.Tests
        ├── MyCoolNewLib.Tests.fsproj
        └── Tests.fs
```

### Build!

```
build.sh
```

The bin of your new lib should look similar to:

```
$ tree src/MyCoolNewLib/bin/
src/MyCoolNewLib/bin/
└── Release
    ├── net45
    │   ├── FSharp.Core.dll
    │   ├── MyCoolNewLib.dll
    │   └── MyCoolNewLib.pdb
    └── netstandard1.6
        ├── MyCoolNewLib.deps.json
        ├── MyCoolNewLib.dll
        └── MyCoolNewLib.pdb

```

### Watch Tests

The `WatchTests` target will use [dotnet-watch](https://github.com/aspnet/Docs/blob/master/aspnetcore/tutorials/dotnet-watch.md) to watch for changes in your lib or tests and re-run your tests on all `TargetFrameworks`

### Release!
* Start a git repo with a remote

```
git add .
git commit -m "Scaffold"
git remote add origin origin https://github.com/user/MyCoolNewLib.git
git push -u origin master
```

* [Add your nuget key to paket](https://fsprojects.github.io/Paket/paket-config.html#Adding-a-NuGet-API-key)


* Then update the `RELEASE_NOTES.md` with a new version, date, and release notes [ReleaseNotesHelper](https://fsharp.github.io/FAKE/apidocs/fake-releasenoteshelper.html)

```
#### 0.2.0 - 30.04.2017
* FEATURE: Does cool stuff!
* BUGFIX: Fixes that silly oversight
```

* You can then use the `Release` target.  This will:
    * make a commit bumping the version:  `Bump version to 0.2.0`
    * publish the pacakge to nuget
    * push a git tag  

```
./build.sh Release
```

