---
title: Adding a library project
category: How To Guides
categoryindex: 1
index: 100
---

# Adding a library

This tutorial will show you how to add a library to your solution.  We will be using the `projLib` argument to create a library called `MyNewLibrary`.  This template assumes you've already created a solution using the [Tutorial](../Tutorials/Getting_Started_With_Libraries.md).

## Steps

1. Open a terminal and navigate to the `src/` directory of your project:
    - `cd src/`
2. Run `dotnet new`  to create a new library project.
    `dotnet new mini-scaffold -n MyNewLibrary --outputType projLib`
3. Navigate back to thr root of the repository and add the project to the solution file.
    - `cd ..`
    - `dotnet sln add src/MyNewLibrary/MyNewLibrary.fsproj`

## Further Reading

- To see adding a corresponding test project see [Adding a testing project](add-a-testing-project.md).
- To see adding a corresponding console project see [Adding a console project](add-a-console-project.md).
- [Microsoft.NET class libraries](https://learn.microsoft.com/en-us/dotnet/standard/class-libraries)
- [F# component design guidelines](https://learn.microsoft.com/en-us/dotnet/fsharp/style-guide/component-design-guidelines)
