# Test platform

The root tests and the generated tests run on VSTest.
`Microsoft.NET.Test.Sdk` starts VSTest, and YoloDev.Expecto.TestSdk 0.16.1 is the Expecto adapter.
No `global.json` selects Microsoft.Testing.Platform (MTP).

```mermaid
flowchart LR
    DotnetTest["dotnet test"] --> VSTestTarget["MSBuild VSTest target"]
    VSTestTarget --> AltCover["AltCover replaces VSTest target (generated tests)"]
    VSTestTarget --> Adapter["expecto.visualstudio.dotnetcore.testadapter.dll"]
    AltCover --> Adapter
    Adapter --> Expecto["Expecto tests in the Exe"]
```

Each test project is an Expecto `Exe` with its own entry point (`Content/Library/tests/MyLib.1.Tests/Main.fs`):

```fsharp
[<EntryPoint>]
let main argv =
    Tests.runTestsInAssemblyWithCLIArgs [] argv
```

## Why the tests do not use MTP

AltCover blocks MTP for the generated tests.
Issue https://github.com/TheAngryByrd/MiniScaffold/issues/356 holds the evidence with source links.

- AltCover 9.0.189 replaces the `VSTest` MSBuild target to instrument and collect (`nupkg/build/AltCover.proj` in the AltCover source).
- In MTP mode, `dotnet test` does not run the `VSTest` target. `-p:AltCover=true` then writes no coverage file, the tests still pass, and `AltCoverThreshold` does not run.
- Upstream issue https://github.com/SteveGilham/altcover/issues/270 tracks MTP support.

## What changes on MTP

The adapter supports MTP in 0.16.1, through `Microsoft.Testing.Extensions.VSTestBridge` 2.3.3.
Expecto 11.1.1 has no MTP code of its own.
A move to MTP must handle these differences:

| Item | VSTest (current) | MTP |
| --- | --- | --- |
| Opt-in | none | `"test": { "runner": "Microsoft.Testing.Platform" }` in `global.json` and `EnableExpectoTestingPlatformIntegration=true` |
| Entry point | own `[<EntryPoint>]`, `GenerateProgramFile=false` | MTP generates one. A custom entry point needs `GenerateTestingPlatformEntryPoint=false` |
| TRX | `--logger trx` | `Microsoft.Testing.Extensions.TrxReport` and `--report-trx`. `UnitTest/@storage` keeps the TFM output path |
| Expecto settings | inline `-- Expecto.fail-on-focused-tests=true` | `--settings` file. Inline settings fail with exit code 5 |
| Discovery output | `The following Tests are available:` | `Discovered N tests in assembly - ...` |
| Coverage | AltCover through the `VSTest` target | none from AltCover |

`build/TestPartitions.ps1` parses the VSTest discovery output and passes inline Expecto settings.
`tests/MiniScaffold.Tests/Main.fs` has a worker mode for the process capture tests, so that project needs a custom MTP entry point.

## Invariants

- Keep `Microsoft.NET.Test.Sdk` in each test project while the tests run on VSTest.
- Do not select MTP in a `global.json` that covers a project with `-p:AltCover=true`. Coverage then stops without an error.

## Related

- [summary.md](summary.md)
- [test-partitions.md](test-partitions.md)
- [../template/dependencies.md](../template/dependencies.md)
- [../build/fake-targets.md](../build/fake-targets.md)
