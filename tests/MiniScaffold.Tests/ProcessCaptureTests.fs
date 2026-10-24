namespace MiniScaffold.Tests

module ProcessCaptureTests =
    open System
    open System.Diagnostics
    open System.IO
    open System.Threading
    open System.Threading.Tasks
    open Expecto
    open Infrastructure

    let private startInfo directory arguments =
        let start =
            ProcessStartInfo(
                "dotnet",
                WorkingDirectory = directory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Text.Encoding.UTF8,
                StandardErrorEncoding = Text.Encoding.UTF8
            )

        arguments
        |> List.iter start.ArgumentList.Add

        start.Environment.Remove "MSBuildSDKsPath"
        |> ignore

        start.Environment.Remove "MSBuildExtensionsPath"
        |> ignore

        start.Environment["MSBUILDDISABLENODEREUSE"] <- "1"
        start

    let private fixture =
        lazy
            (let directory = Disposables.DisposableDirectory.Create()
             AppDomain.CurrentDomain.ProcessExit.Add(fun _ -> Disposables.dispose directory)

             for sourceName, targetName in
                 [
                     "ProcessCaptureFixture.csproj.template", "ProcessCaptureFixture.csproj"
                     "Program.cs", "Program.cs"
                 ] do
                 File.Copy(
                     Path.Combine(__SOURCE_DIRECTORY__, "Fixtures/ProcessCapture", sourceName),
                     Path.Combine(directory.Directory, targetName)
                 )

             let build =
                 startInfo directory.Directory [
                     "build"
                     "ProcessCaptureFixture.csproj"
                     "--output"
                     "out"
                 ]

             let result = ProcessCapture.run (TimeSpan.FromSeconds 30.) build

             Expect.equal
                 result.ExitCode
                 0
                 (sprintf
                     "Fixture build failed.\nstdout:\n%s\nstderr:\n%s"
                     result.StandardOutput
                     result.StandardError)

             Path.Combine(directory.Directory, "out/ProcessCaptureFixture.dll"))

    let private fixtureStart directory arguments =
        startInfo
            directory
            (fixture.Value
             :: arguments)

    let private parentOutput = "parent-stdout\r\nline\rbare\n\U0001F680-tail"
    let private parentError = "parent-stderr\r\nline\rbare\n\u03a9-tail"

    let private exactOutput =
        String('o', 8191)
        + "\U0001F680\r\nstdout\rtail\nno-newline"

    let private exactError =
        String('e', 4095)
        + "\U0001F680\r\nstderr\rtail\nno-newline"

    let private diagnosticsDirectory () =
        Path.Combine(Builds.diagnosticsRoot, Guid.NewGuid().ToString("N"))

    let private writeFixtureBuildScripts directory arguments =
        let assembly = fixture.Value

        let arguments =
            arguments
            |> List.map (sprintf "\"%s\"")
            |> String.concat " "

        File.WriteAllText(
            Path.Combine(directory, "build.cmd"),
            sprintf "@echo off\r\ndotnet \"%s\" %s\r\nexit /b %%errorlevel%%\r\n" assembly arguments
        )

        File.WriteAllText(
            Path.Combine(directory, "build.sh"),
            sprintf "#!/usr/bin/env bash\ndotnet \"%s\" %s\n" assembly arguments
        )

    let private writeBuildScripts directory exitCode =
        writeFixtureBuildScripts directory [
            "streams"
            "0"
            string exitCode
        ]

    let private readLiveText path =
        use stream =
            new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite
                ||| FileShare.Delete
            )

        use reader = new StreamReader(stream)
        reader.ReadToEnd()

    let private expectLiveText path expected =
        Expect.isTrue
            (SpinWait.SpinUntil(
                (fun () ->
                    File.Exists path
                    && readLiveText path = expected
                ),
                TimeSpan.FromSeconds 30.
            ))
            (sprintf "Expected exact output in %s while the child was still running" path)

    let private attempt action =
        try
            Ok(action ())
        with ex ->
            Error ex

    let private failure result =
        match result with
        | Error ex -> ex
        | Ok _ -> failtest "The command should fail"

    let private waitForFile directory name =
        let path = Path.Combine(directory, name)

        Expect.isTrue
            (SpinWait.SpinUntil((fun () -> File.Exists path), TimeSpan.FromSeconds 30.))
            (sprintf "Fixture did not create %s" path)

    let private descendantProcess directory =
        waitForFile directory "ready"

        let pid =
            Int32.Parse(
                readLiveText (Path.Combine(directory, "ready")),
                Globalization.CultureInfo.InvariantCulture
            )

        let descendant = Process.GetProcessById pid

        descendant.Handle
        |> ignore

        descendant

    let private releaseDescendant directory (descendant: Process) =
        File.WriteAllText(Path.Combine(directory, "release"), "release")
        descendant.WaitForExit()
        Expect.isTrue descendant.HasExited "The owned descendant must exit before directory cleanup"
        waitForFile directory "complete"

        Expect.equal
            (File.ReadAllText(Path.Combine(directory, "complete")))
            "completed naturally"
            "The descendant must finish without cancellation"

    let private oldCapture
        (exited: ManualResetEventSlim)
        (start: ProcessStartInfo)
        : ProcessCapture.ProcessOutput =
        use child = new Process(StartInfo = start)
        Expect.isTrue (child.Start()) "The old-helper control should start"
        let stdout = child.StandardOutput.ReadToEndAsync()
        let stderr = child.StandardError.ReadToEndAsync()
        child.WaitForExit()
        exited.Set()

        {
            ExitCode = child.ExitCode
            StandardOutput = stdout.Result
            StandardError = stderr.Result
        }

    [<Tests>]
    let tests =
        testList "process capture" [
            testCase "keeps success streams separate"
            <| fun _ ->
                use directory = Disposables.DisposableDirectory.Create()

                let result =
                    fixtureStart directory.Directory [ "exact" ]
                    |> ProcessCapture.run (TimeSpan.FromSeconds 1.)

                Expect.equal result.ExitCode 0 "The fixture should succeed"

                Expect.equal
                    result.StandardOutput
                    exactOutput
                    "stdout must preserve Unicode, carriage returns, line feeds, and its missing final newline"

                Expect.equal
                    result.StandardError
                    exactError
                    "stderr must preserve its exact text separately"

            testCase "returns nonzero exits to callers"
            <| fun _ ->
                use directory = Disposables.DisposableDirectory.Create()

                let result =
                    fixtureStart directory.Directory [
                        "streams"
                        "0"
                        "17"
                    ]
                    |> ProcessCapture.run (TimeSpan.FromSeconds 1.)

                Expect.equal result.ExitCode 17 "Capture should preserve a nonzero exit"

                Expect.equal
                    (result.StandardOutput.Trim())
                    "fixture-stdout"
                    "Nonzero exit should preserve stdout"

                Expect.equal
                    (result.StandardError.Trim())
                    "fixture-stderr"
                    "Nonzero exit should preserve stderr"

            testCase "waits for the direct child beyond the drain limit"
            <| fun _ ->
                use directory = Disposables.DisposableDirectory.Create()
                let elapsed = Stopwatch.StartNew()

                let result =
                    fixtureStart directory.Directory [
                        "streams"
                        "3000"
                        "0"
                    ]
                    |> ProcessCapture.run (TimeSpan.FromSeconds 1.)

                Expect.equal result.ExitCode 0 "A slow direct child should complete"

                Expect.isGreaterThanOrEqual
                    elapsed.Elapsed
                    (TimeSpan.FromSeconds 3.)
                    "The drain limit must not cancel the direct child"

                Expect.equal
                    (result.StandardOutput.Trim())
                    "fixture-stdout"
                    "The child should reach its final output"

            testCase "contextualizes output task faults after exit"
            <| fun _ ->
                use directory = Disposables.DisposableDirectory.Create()

                let start =
                    fixtureStart directory.Directory [
                        "faulting"
                        directory.Directory
                    ]

                start.StandardOutputEncoding <- Text.UTF8Encoding(false, true)
                let stdoutPath = Path.Combine(directory.Directory, "stdout.txt")
                let stderrPath = Path.Combine(directory.Directory, "stderr.txt")

                let stdout =
                    new StreamWriter(stdoutPath, false, Text.UTF8Encoding(false), AutoFlush = true)

                let stderr =
                    new StreamWriter(stderrPath, false, Text.UTF8Encoding(false), AutoFlush = true)

                let run =
                    Task.Run(fun () ->
                        attempt (fun () ->
                            ProcessCapture.runWithOutput
                                stdout
                                stderr
                                (TimeSpan.FromSeconds 1.)
                                start
                        )
                    )

                try
                    waitForFile directory.Directory "fault-ready"
                    expectLiveText stdoutPath parentOutput
                    expectLiveText stderrPath parentError
                finally
                    File.WriteAllText(Path.Combine(directory.Directory, "release"), "release")
                    run.Wait()

                let error = failure run.Result

                for detail in
                    [
                        "dotnet"
                        "faulting"
                        directory.Directory
                        "exited with code 19"
                        "output draining failed"
                    ] do
                    Expect.stringContains
                        error.Message
                        detail
                        "Output read faults must preserve command, directory, exit, and fault context"

                Expect.isNotNull error.InnerException "Output read faults should retain their cause"

                Expect.stringContains
                    error.Message
                    parentOutput
                    "Read faults must preserve exact partial stdout"

                Expect.stringContains
                    error.Message
                    parentError
                    "Read faults must preserve exact partial stderr"

                Expect.equal
                    (File.ReadAllText stdoutPath)
                    parentOutput
                    "The valid stdout prefix must remain in its durable file"

                Expect.equal
                    (File.ReadAllText stderrPath)
                    parentError
                    "The stderr prefix must remain in its durable file"

            testCase "bounds inherited pipes after exit without cancelling the descendant"
            <| fun _ ->
                use directory = Disposables.DisposableDirectory.Create()

                let start =
                    fixtureStart directory.Directory [
                        "parent"
                        directory.Directory
                    ]

                let run =
                    Task.Run(fun () ->
                        attempt (fun () -> ProcessCapture.run (TimeSpan.FromSeconds 1.) start)
                    )

                use descendant = descendantProcess directory.Directory

                try
                    waitForFile directory.Directory "parent-exiting"

                    Expect.isTrue
                        (run.Wait(TimeSpan.FromSeconds 10.))
                        "Capture must fail while the descendant still holds both pipes"

                    let error = failure run.Result

                    Expect.isTrue
                        (error :? TimeoutException)
                        (sprintf "Expected a drain timeout, got %O" error)

                    for detail in
                        [
                            "dotnet"
                            "parent"
                            directory.Directory
                            "exited with code 23"
                            "1 seconds after exit"
                        ] do
                        Expect.stringContains
                            error.Message
                            detail
                            "The drain failure must retain command, directory, exit, and timeout"

                    Expect.stringContains
                        error.Message
                        parentOutput
                        "The timeout must preserve exact partial stdout"

                    Expect.stringContains
                        error.Message
                        parentError
                        "The timeout must preserve exact partial stderr"

                    Expect.isFalse
                        (File.Exists(Path.Combine(directory.Directory, "complete")))
                        "The capture failure must precede descendant completion"
                finally
                    releaseDescendant directory.Directory descendant

            testCase "old helper misses the bound and then completes naturally"
            <| fun _ ->
                use directory = Disposables.DisposableDirectory.Create()

                let start =
                    fixtureStart directory.Directory [
                        "parent"
                        directory.Directory
                    ]

                use exited = new ManualResetEventSlim()
                let run = Task.Run(fun () -> oldCapture exited start)
                use descendant = descendantProcess directory.Directory

                try
                    waitForFile directory.Directory "parent-exiting"

                    Expect.isTrue
                        (exited.Wait(TimeSpan.FromSeconds 5.))
                        "The old-helper child must exit before checking its output wait"

                    Expect.isFalse
                        (run.Wait(TimeSpan.FromSeconds 2.))
                        "The old helper should still be blocked beyond the new drain limit"
                finally
                    releaseDescendant directory.Directory descendant

                Expect.isTrue
                    (run.Wait(TimeSpan.FromSeconds 5.))
                    "The old helper must complete naturally after pipe closure"

                Expect.equal
                    run.Result.ExitCode
                    23
                    "The old helper should preserve the direct-child exit"

                Expect.equal
                    run.Result.StandardOutput
                    parentOutput
                    "The old helper should eventually read stdout"

                Expect.equal
                    run.Result.StandardError
                    parentError
                    "The old helper should eventually read stderr"

            testCase "build failure retains streams and nested TRX before cleanup"
            <| fun _ ->
                let generated = Disposables.DisposableDirectory.Create()
                let diagnostics = diagnosticsDirectory ()
                let marker = Guid.NewGuid().ToString("N")
                let nested = Path.Combine(generated.Directory, "tests/Example.Tests/TestResults")

                Directory.CreateDirectory nested
                |> ignore

                File.WriteAllText(Path.Combine(nested, "nested.trx"), marker)

                File.WriteAllText(
                    Path.Combine(generated.Directory, "unselected.txt"),
                    "do not copy"
                )

                writeBuildScripts generated.Directory 17

                let error =
                    try
                        attempt (fun () ->
                            Builds.executeBuildWithDiagnostics
                                diagnostics
                                (TimeSpan.FromSeconds 1.)
                                generated.Directory
                                "ControlledFailure"
                        )
                        |> failure
                    finally
                        Disposables.dispose generated

                for detail in
                    [
                        "build."
                        "ControlledFailure"
                        generated.Directory
                        "exited with code 17"
                        "stdout:\nfixture-stdout"
                        "stderr:\nfixture-stderr"
                    ] do
                    Expect.stringContains
                        error.Message
                        detail
                        "Build failure must preserve the process context and both streams"

                Expect.isFalse
                    (Directory.Exists generated.Directory)
                    "The generated directory should be removed"

                let retained =
                    Directory.EnumerateFiles(diagnostics, "nested.trx", SearchOption.AllDirectories)
                    |> Seq.toList

                Expect.hasLength
                    retained
                    1
                    "Exactly one nested result must survive generated-directory cleanup"

                Expect.equal
                    (File.ReadAllText(
                        Path.Combine(diagnostics, "tests/Example.Tests/TestResults/nested.trx")
                    ))
                    marker
                    "The owned nested result must preserve its bytes"

                let diagnostic = Path.Combine(diagnostics, "process.txt")

                Expect.stringContains
                    error.Message
                    (Path.GetDirectoryName diagnostic)
                    "The assertion should locate retained diagnostics"

                Expect.isFalse
                    (File.Exists(Path.Combine(Path.GetDirectoryName diagnostic, "unselected.txt")))
                    "Retention must not copy the full generated tree"

                for detail in
                    [
                        "exited with code 17"
                        "fixture-stdout"
                        "fixture-stderr"
                    ] do
                    Expect.stringContains
                        (File.ReadAllText diagnostic)
                        detail
                        "Retained process diagnostics must preserve the original failure"

            testCase "retention failure preserves the original build failure"
            <| fun _ ->
                use directory = Disposables.DisposableDirectory.Create()
                let diagnostics = diagnosticsDirectory ()
                let nested = Path.Combine(directory.Directory, "TestResults")

                Directory.CreateDirectory nested
                |> ignore

                let trx = Path.Combine(nested, "blocked.trx")
                File.WriteAllText(trx, "test result")

                Directory.CreateDirectory(Path.Combine(diagnostics, "TestResults/blocked.trx"))
                |> ignore

                writeBuildScripts directory.Directory 17

                let error =
                    attempt (fun () ->
                        Builds.executeBuildWithDiagnostics
                            diagnostics
                            (TimeSpan.FromSeconds 1.)
                            directory.Directory
                            "ControlledFailure"
                    )
                    |> failure

                for detail in
                    [
                        "exited with code 17"
                        "stdout:\nfixture-stdout"
                        "stderr:\nfixture-stderr"
                        "Could not retain all diagnostics"
                    ] do
                    Expect.stringContains
                        error.Message
                        detail
                        "Retention errors must not replace the original process failure"

            testCase "successful build retains output without copying nested results"
            <| fun _ ->
                use directory = Disposables.DisposableDirectory.Create()
                let diagnostics = diagnosticsDirectory ()

                Directory.CreateDirectory(Path.Combine(directory.Directory, "TestResults"))
                |> ignore

                File.WriteAllText(
                    Path.Combine(directory.Directory, "TestResults/success.trx"),
                    "success marker"
                )

                writeBuildScripts directory.Directory 0

                Builds.executeBuildWithDiagnostics
                    diagnostics
                    (TimeSpan.FromSeconds 1.)
                    directory.Directory
                    "ControlledSuccess"

                Expect.stringContains
                    (File.ReadAllText(Path.Combine(diagnostics, "process.txt")))
                    "exited with code 0"
                    "The owned completion record should report success"

                Expect.equal
                    (File.ReadAllText(Path.Combine(diagnostics, "stdout.txt")))
                    ("fixture-stdout"
                     + Environment.NewLine)
                    "Successful stdout should remain available"

                Expect.equal
                    (File.ReadAllText(Path.Combine(diagnostics, "stderr.txt")))
                    ("fixture-stderr"
                     + Environment.NewLine)
                    "Successful stderr should remain available"

                Expect.isFalse
                    (File.Exists(Path.Combine(diagnostics, "TestResults/success.trx")))
                    "Successful builds should not copy nested failure results"

            testCase "writes exact durable output while the direct child is running"
            <| fun _ ->
                use directory = Disposables.DisposableDirectory.Create()
                let diagnostics = diagnosticsDirectory ()

                writeFixtureBuildScripts directory.Directory [
                    "running"
                    directory.Directory
                ]

                let run =
                    Task.Run(fun () ->
                        attempt (fun () ->
                            Builds.executeBuildWithDiagnostics
                                diagnostics
                                (TimeSpan.FromSeconds 1.)
                                directory.Directory
                                "ControlledRunning"
                        )
                    )

                try
                    waitForFile directory.Directory "running-ready"
                    expectLiveText (Path.Combine(diagnostics, "stdout.txt")) exactOutput
                    expectLiveText (Path.Combine(diagnostics, "stderr.txt")) exactError

                    Expect.isFalse
                        run.IsCompleted
                        "Durable output must be available before the direct child exits"
                finally
                    File.WriteAllText(Path.Combine(directory.Directory, "release"), "release")
                    run.Wait()

                match run.Result with
                | Error ex -> failtestf "The released direct child should succeed: %O" ex
                | Ok() -> ()

                Expect.equal
                    (File.ReadAllText(Path.Combine(diagnostics, "stdout.txt")))
                    exactOutput
                    "The durable stdout file must preserve exact text"

                Expect.equal
                    (File.ReadAllText(Path.Combine(diagnostics, "stderr.txt")))
                    exactError
                    "The durable stderr file must preserve exact text"

            testCase "retains exact partial build output after a drain timeout"
            <| fun _ ->
                use directory = Disposables.DisposableDirectory.Create()
                let diagnostics = diagnosticsDirectory ()

                writeFixtureBuildScripts directory.Directory [
                    "parent"
                    directory.Directory
                ]

                let run =
                    Task.Run(fun () ->
                        attempt (fun () ->
                            Builds.executeBuildWithDiagnostics
                                diagnostics
                                (TimeSpan.FromSeconds 1.)
                                directory.Directory
                                "ControlledDrain"
                        )
                    )

                use descendant = descendantProcess directory.Directory

                try
                    waitForFile directory.Directory "parent-exiting"

                    Expect.isTrue
                        (run.Wait(TimeSpan.FromSeconds 10.))
                        "Build failure must return while the descendant retains the pipes"

                    let error = failure run.Result

                    Expect.isTrue
                        (error.InnerException :? TimeoutException)
                        "The retained build failure should preserve the drain timeout cause"

                    Expect.stringContains
                        error.Message
                        diagnostics
                        "The build timeout must identify its retained diagnostics"

                    let retained = File.ReadAllText(Path.Combine(diagnostics, "process.txt"))

                    for text in
                        [
                            parentOutput
                            parentError
                            "exited with code 23"
                        ] do
                        Expect.stringContains
                            error.Message
                            text
                            "The build timeout must preserve its exact streams and context"

                        Expect.stringContains
                            retained
                            text
                            "The completed process record must preserve the failure"

                    expectLiveText (Path.Combine(diagnostics, "stdout.txt")) parentOutput
                    expectLiveText (Path.Combine(diagnostics, "stderr.txt")) parentError
                finally
                    releaseDescendant directory.Directory descendant
        ]
