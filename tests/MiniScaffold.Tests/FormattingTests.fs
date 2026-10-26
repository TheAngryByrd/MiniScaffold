namespace MiniScaffold.Tests

module FormattingTests =
    open System
    open System.Diagnostics
    open System.IO
    open System.Security.Cryptography
    open Expecto
    open Infrastructure

    type private Host =
        | Root
        | Library
        | Console

    type private CommandResult = { ExitCode: int; Output: string }

    let private repository =
        Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "../.."))

    let private templateSetup =
        lazy
            (Tests.tests
             |> ignore)

    let private run directory executable arguments =
        let start =
            ProcessStartInfo(
                executable,
                WorkingDirectory = directory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            )

        for argument in arguments do
            start.ArgumentList.Add argument

        start.Environment.["DOTNET_CLI_HOME"] <- Path.Combine(directory, ".cli-home")
        start.Environment.["NUGET_HTTP_CACHE_PATH"] <- Path.Combine(directory, ".http-cache")
        start.Environment.["DOTNET_NOLOGO"] <- "1"
        start.Environment.["CI"] <- "false"
        start.Environment.["MSBUILDDISABLENODEREUSE"] <- "1"

        start.Environment.Remove "MSBuildSDKsPath"
        |> ignore

        start.Environment.Remove "MSBuildExtensionsPath"
        |> ignore

        use child = Process.Start start
        let output = child.StandardOutput.ReadToEndAsync()
        let errors = child.StandardError.ReadToEndAsync()
        child.WaitForExit()

        {
            ExitCode = child.ExitCode
            Output =
                output.GetAwaiter().GetResult()
                + errors.GetAwaiter().GetResult()
        }

    let private succeeds result =
        Expect.equal result.ExitCode 0 result.Output

    let private fails result =
        Expect.notEqual result.ExitCode 0 result.Output

    let private copyFile source (destination: string) =
        Directory.CreateDirectory(Path.GetDirectoryName destination)
        |> ignore

        File.Copy(source, destination, true)

    let private createHost host directory =
        Directory.CreateDirectory directory
        |> ignore

        match host with
        | Root ->
            for file in
                [
                    "Directory.Build.props"
                    "Directory.Build.targets"
                    "Directory.Solution.targets"
                    "Directory.Packages.props"
                    "global.json"
                    ".config/dotnet-tools.json"
                    ".editorconfig"
                    ".fantomasignore"
                    "build.cmd"
                    "build.sh"
                    "CHANGELOG.md"
                ] do
                copyFile (Path.Combine(repository, file)) (Path.Combine(directory, file))

            for file in Directory.GetFiles(Path.Combine(repository, "build")) do
                copyFile file (Path.Combine(directory, "build", Path.GetFileName file))
        | Library
        | Console ->
            templateSetup.Force()

            let outputType =
                string host
                |> _.ToLowerInvariant()

            Tests.runTemplate
                directory
                $"--name FormattingProbe --githubUsername TestUser --outputType {outputType} --output ."

    let private msbuild directory project target properties =
        run
            directory
            "dotnet"
            ([
                "msbuild"
                project
                $"-t:{target}"
                "-m:1"
                "-v:normal"
             ]
             @ properties)

    let private formatTarget =
        function
        | Root -> "Format"
        | Library
        | Console -> "FormatFSharp"

    let private stamps directory pattern =
        if Directory.Exists directory then
            Directory.GetFiles(directory, pattern)
            |> Array.filter (fun path -> not (path.EndsWith ".inputs"))
        else
            [||]

    let private onlyStamp directory pattern =
        let found = stamps directory pattern
        Expect.equal found.Length 1 $"Expected one {pattern} stamp in {directory}: {found}"
        found.[0]

    let private sourceState directory =
        Directory.GetFiles(directory, "*", SearchOption.AllDirectories)
        |> Seq.filter (fun path ->
            let parts = Path.GetRelativePath(directory, path).Split(Path.DirectorySeparatorChar)

            List.contains (Path.GetExtension path) [
                ".fs"
                ".fsi"
                ".fsx"
            ]
            && not (
                parts
                |> Array.exists (fun part ->
                    part = "bin"
                    || part = "obj"
                )
            )
        )
        |> Seq.map (fun path ->
            Path.GetRelativePath(directory, path),
            File.ReadAllBytes path
            |> SHA256.HashData
        )
        |> Map.ofSeq

    let private incrementalFormatting host _ =
        use temp = Disposables.DisposableDirectory.Create()
        let directory = Path.Combine(temp.Directory, $"{host} with spaces")
        createHost host directory
        let project = "build/build.fsproj"
        let target = formatTarget host
        let nested = Path.Combine(directory, "build", "nested directory", "Probe.fsx")

        Directory.CreateDirectory(Path.GetDirectoryName nested)
        |> ignore

        File.WriteAllText(nested, "let value=1\n")

        msbuild directory project target [ "-p:DesignTimeBuild=true" ]
        |> succeeds

        Expect.isEmpty
            (stamps (Path.Combine(directory, "build/obj")) "dotnet-*")
            "Design-time formatting must not create stamps"

        msbuild directory project "BeforeBuild" [ "-p:FormatOnBuild=false" ]
        |> succeeds

        Expect.equal
            (File.ReadAllText nested)
            "let value=1\n"
            "Disabled formatting must preserve nested source"

        msbuild directory project target []
        |> succeeds

        Expect.equal
            (File.ReadAllText nested)
            "let value = 1\n"
            "Formatting must recurse into paths with spaces"

        let stamp = onlyStamp (Path.Combine(directory, "build/obj")) "dotnet-fantomas-*"
        let first = File.GetLastWriteTimeUtc stamp

        msbuild directory project target []
        |> succeeds

        Expect.equal
            (File.GetLastWriteTimeUtc stamp)
            first
            "An unchanged second call must skip formatting"

        let config = Path.Combine(Path.GetDirectoryName nested, ".editorconfig")
        File.WriteAllText(config, "[*.fsx]\nfsharp_space_before_lowercase_invocation = false\n")
        File.WriteAllText(nested, "printfn (\"value\")\n")
        File.SetLastWriteTimeUtc(nested, first.AddSeconds(-2.0))

        msbuild directory project target []
        |> succeeds

        Expect.notEqual
            (File.ReadAllText nested)
            "printfn (\"value\")\n"
            "A new nested configuration must invalidate formatting"

        let formatted = File.GetLastWriteTimeUtc stamp
        File.WriteAllText(nested, "let broken=(\n")

        msbuild directory project target []
        |> fails

        Expect.equal
            (File.GetLastWriteTimeUtc stamp)
            formatted
            "Parser failure must not advance the success stamp"

        msbuild directory project "BeforeBuild" [ "-p:FormatOnBuild=false" ]
        |> succeeds

        msbuild directory project "BeforeBuild" [ "-p:DesignTimeBuild=true" ]
        |> succeeds

        Expect.equal
            (File.ReadAllText nested)
            "let broken=(\n"
            "Excluded builds must preserve malformed source"

        Expect.equal
            (File.GetLastWriteTimeUtc stamp)
            formatted
            "Excluded builds must preserve the success stamp"

        match host with
        | Root -> ()
        | Library
        | Console ->
            msbuild directory "src/FormattingProbe/FormattingProbe.fsproj" "ToolRestore" []
            |> succeeds

            msbuild directory "src/FormattingProbe/FormattingProbe.fsproj" target []
            |> succeeds

    let private restoreOrdering host _ =
        use temp = Disposables.DisposableDirectory.Create()
        let directory = Path.Combine(temp.Directory, $"{host} restore with spaces")
        createHost host directory
        let project = Path.Combine(directory, "RestoreProbe.fsproj")

        File.WriteAllText(
            project,
            """<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup>
  <Target Name="ObserveProjectDispatch" BeforeTargets="_IsProjectRestoreSupported;_GenerateRestoreGraphProjectEntry">
    <Error Condition="!Exists('$(_DotnetToolRestoreOutputFile)')" Text="Project dispatched before tool restore" />
    <WriteLinesToFile File="dispatch-observed" Lines="dispatched" Overwrite="true" />
  </Target>
</Project>"""
        )

        let peerDirectory = Path.Combine(directory, "restore-peer")
        copyFile project (Path.Combine(peerDirectory, "RestorePeer.fsproj"))

        File.WriteAllText(
            Path.Combine(directory, "RestoreProbe.slnx"),
            """<Solution><Project Path="RestoreProbe.fsproj" /><Project Path="restore-peer/RestorePeer.fsproj" /></Solution>"""
        )

        msbuild directory project "ToolRestore" [ "-p:DesignTimeBuild=true" ]
        |> succeeds

        let obj = Path.Combine(directory, "build/obj")

        Expect.isEmpty
            (stamps obj "dotnet-tool-restore-*")
            "Design-time tool restore must not write a stamp"

        msbuild directory "RestoreProbe.slnx" "Restore" [ "-m:2" ]
        |> succeeds

        Expect.isTrue
            (File.Exists(Path.Combine(directory, "dispatch-observed")))
            "The first project dispatch sentinel must execute"

        Expect.isTrue
            (File.Exists(Path.Combine(peerDirectory, "dispatch-observed")))
            "The second project dispatch sentinel must execute"

        let stamp = onlyStamp obj "dotnet-tool-restore-*"
        let first = File.GetLastWriteTimeUtc stamp

        msbuild directory "RestoreProbe.slnx" "Restore" [ "-m:2" ]
        |> succeeds

        Expect.equal
            (File.GetLastWriteTimeUtc stamp)
            first
            "Repeated solution restore must reuse the shared stamp"

        File.Delete stamp

        msbuild directory project "Restore" []
        |> succeeds

        Expect.isTrue (File.Exists stamp) "Direct project restore must restore tools"

        let previous = File.GetLastWriteTimeUtc stamp
        File.WriteAllText(Path.Combine(directory, ".config/dotnet-tools.json"), "invalid manifest")

        msbuild directory project "ToolRestore" []
        |> fails

        Expect.equal
            (File.GetLastWriteTimeUtc stamp)
            previous
            "Failed restore must not advance its stamp"

    let private formatRoutes host _ =
        use temp = Disposables.DisposableDirectory.Create()
        let directory = Path.Combine(temp.Directory, $"{host} build routes with spaces")
        createHost host directory
        let source = Path.Combine(directory, "RouteProbe.fs")
        let project = Path.Combine(directory, "RouteProbe.fsproj")

        File.WriteAllText(
            project,
            """<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFrameworks>net8.0;net9.0;net10.0</TargetFrameworks></PropertyGroup>
  <ItemGroup><Compile Include="RouteProbe.fs" /><PackageReference Include="FSharp.Core" /></ItemGroup>
</Project>"""
        )

        let assertFormatsOnce target properties =
            File.WriteAllText(source, "module RouteProbe\nlet value=1\n")
            let result = msbuild directory project target properties
            succeeds result

            Expect.equal
                (File.ReadAllText source)
                "module RouteProbe\n\nlet value = 1\n"
                result.Output

            let invocations =
                result.Output.Split('\n')
                |> Array.filter (fun line -> line.TrimStart().StartsWith "dotnet fantomas ")

            Expect.equal invocations.Length 1 $"Expected one formatter invocation: {result.Output}"

        File.WriteAllText(source, "module RouteProbe\nlet value=1\n")

        msbuild directory project "Restore" []
        |> succeeds

        assertFormatsOnce "Build" []
        let stamp = onlyStamp (Path.Combine(directory, "obj")) "dotnet-fantomas-*"
        let first = File.GetLastWriteTimeUtc stamp

        msbuild directory project "Build" []
        |> succeeds

        Expect.equal
            (File.GetLastWriteTimeUtc stamp)
            first
            "An unchanged multi-target build must skip formatting"

        assertFormatsOnce "Build" [ "-p:TargetFramework=net8.0" ]
        assertFormatsOnce "Rebuild" []
        assertFormatsOnce "Publish" [ "-p:TargetFramework=net8.0" ]

        msbuild directory project "Clean" []
        |> succeeds

        Expect.isEmpty
            (stamps (Path.Combine(directory, "obj")) "dotnet-fantomas-*")
            "Clean must remove the formatting stamp"

        Expect.isFalse
            (File.Exists(
                stamp
                + ".inputs"
            ))
            "Clean must remove the recorded input list"

    let private publicCheck host _ =
        use temp = Disposables.DisposableDirectory.Create()
        let directory = Path.Combine(temp.Directory, $"{host} public wrapper with spaces")
        createHost host directory
        let buildSource = Path.Combine(directory, "build/build.fs")
        let original = File.ReadAllText buildSource
        let dirty = original.Replace("let productName =", "let productName=")
        Expect.notEqual dirty original "The bootstrap source must contain a real formatting defect"
        File.WriteAllText(buildSource, dirty)

        let wrapper target =
            if OperatingSystem.IsWindows() then
                run directory "cmd.exe" [
                    "/d"
                    "/c"
                    "build.cmd"
                    target
                ]
            else
                run directory "bash" [
                    "./build.sh"
                    target
                ]

        let before = sourceState directory
        let check = wrapper "CheckFormatCode"
        fails check

        Expect.stringContains
            check.Output
            "Some files need formatting"
            "The check must reach Fantomas"

        Expect.equal
            (sourceState directory)
            before
            "The public check must preserve all source, including its bootstrap"

        wrapper "FormatCode"
        |> succeeds

        Expect.notEqual (sourceState directory) before "Explicit formatting must repair source"

        wrapper "CheckFormatCode"
        |> succeeds

        let malformed = Path.Combine(directory, "build/Broken.fsx")
        File.WriteAllText(malformed, "let broken=(\n")
        let beforeFailure = sourceState directory
        let parserCheck = wrapper "CheckFormatCode"
        fails parserCheck

        Expect.stringContains
            parserCheck.Output
            "Fantomas failed with exit code 1"
            "The parser failure must reach FAKE"

        Expect.equal (sourceState directory) beforeFailure "A failed check must preserve source"
        let parserFormat = wrapper "FormatCode"
        fails parserFormat

        Expect.stringContains
            parserFormat.Output
            "Fantomas failed with exit code 1"
            "Explicit formatting must propagate parser failure"

    [<Tests>]
    let tests =
        testSequenced
        <| testList "Formatting and tool restore" [
            for host in
                [
                    Root
                    Library
                    Console
                ] do
                testCase $"{host} incremental formatting" (incrementalFormatting host)
                testCase $"{host} solution restore ordering" (restoreOrdering host)
                testCase $"{host} multi-target build routes" (formatRoutes host)
                testCase $"{host} public read-only check" (publicCheck host)
        ]
