---
title: Adding a testing project
category: Tutorials
categoryindex: 1
index: 300
---

# Adding a Testing project

This tutorial will show you how to add a test project to your solution.  We will be using the `projTest` argument to create a project called `MyNewLibrary.Tests`.  This template assumes you've already created a solution using the [Tutorial](../Tutorials/Getting_Started_With_Libraries.md).

## Steps

1. Open a terminal and navigate to the `tests/` directory of your project:
    - `cd tests/`
2. Run `dotnet new`  to create a new library project.
    `dotnet new mini-scaffold -n MyNewLibrary.Tests --outputType projLib`
3. Navigate back to thr root of the repository and add the project to the solution file.
    - `cd ..`
    - `dotnet sln add src/MyNewLibrary.Tests/MyNewLibrary.Tests.fsproj`
4. Optionally, add a reference to the project you want to test.
    - `dotnet add src/MyNewLibrary.Tests/MyNewLibrary.Tests.fsproj reference src/MyNewLibrary/MyNewLibrary.fsproj`

## Further Reading

- To see adding a corresponding library project see [Adding a testing project](add-a-library-project.md).
- To see adding a corresponding console project see [Adding a console project](add-a-console-project.md).
- MiniScaffold uses [Expecto](https://github.com/haf/expecto) for testing.  See the [Expecto documentation](https://github.com/haf/expecto#testing-hello-world).
- [Unit testing best practices with .NET Core and .NET Standard](https://docs.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices)

