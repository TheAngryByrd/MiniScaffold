---
title: Adding a console project
category: Tutorials
categoryindex: 1
index: 100
---

# Adding a Console Project

This tutorial will show you how to add a library to your solution.  We will be using the `projConsole` argument to create a library called `MyNewConsoleApp`.  This template assumes you've already created a solution using the [Tutorial](../Tutorials/Getting_Started_With_Libraries.md).

## Steps

1. Open a terminal and navigate to the `src/` directory of your project:
    - `cd src/`
2. Run `dotnet new`  to create a new library project.
    `dotnet new mini-scaffold -n MyNewConsoleApp --outputType projConsole`
3. Navigate back to thr root of the repository and add the project to the solution file.
    - `cd ..`
    - `dotnet sln add src/MyNewConsoleApp/MyNewConsoleApp.fsproj`

## Further Reading

- To see adding a corresponding test project see [Adding a testing project](add-a-testing-project.md).
- To see adding a corresponding library project see [Adding a library project](add-a-library-project.md).
- [NET application publishing overview](https://learn.microsoft.com/en-us/dotnet/core/deploying/)
