# MiniScaffold
F# Template for creating and publishing libraries targeting .NET Full (net45) and Core (netstandard1.6)

[![NuGet Badge](https://img.shields.io/nuget/vpre/MiniScaffold.svg)](https://www.nuget.org/packages/MiniScaffold/)

[![Travis Badge](https://travis-ci.org/TheAngryByrd/MiniScaffold.svg?branch=master)](https://travis-ci.org/TheAngryByrd/MiniScaffold)


### Getting started

Grab the template from nuget:

```
dotnet new -i MiniScaffold::*
```

Use the new template:

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

Build!

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
Release!
* Start a git repo with a remote
* Then update the `RELEASE_NOTES.md` with a new version 
* You can then publish to nuget and push a git tag!   

```
./build.sh Release
```

