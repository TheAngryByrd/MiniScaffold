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

    type internal WorkerRequest = private {
        FixtureAssembly: string
        Directory: string
    }

    type internal WorkerDispatch =
        | OrdinaryArguments
        | CaptureWorker of WorkerRequest
        | InvalidWorkerArguments of string

    let private workerArgument = "--miniscaffold-process-capture-starved-worker"

    let internal parseWorkerArguments arguments =
        match arguments with
        | [| token; assembly; directory |] when token = workerArgument ->
            try
                let assembly = Path.GetFullPath assembly
                let directory = Path.GetFullPath directory

                if
                    File.Exists assembly
                    && Directory.Exists directory
                then
                    CaptureWorker {
                        FixtureAssembly = assembly
                        Directory = directory
                    }
                else
                    InvalidWorkerArguments "The prepared fixture and worker directory must exist."
            with ex ->
                InvalidWorkerArguments ex.Message
        | arguments when Array.tryHead arguments = Some workerArgument ->
            InvalidWorkerArguments "The private worker requires a fixture assembly and directory."
        | _ -> OrdinaryArguments

    type private WorkerPhase =
        | Active = 0
        | Verified = 1
        | Recovering = 2

    type private WorkerObservation = {
        CaptureExitCode: int
        StandardOutput: string
        StandardError: string
        CaptureError: string
        DurableOutput: string
        DurableError: string
        MaximumBeforeCapture: int
        AvailableBeforeCapture: int
        MaximumAtVerdict: int
        AvailableAtVerdict: int
        BlockerAcknowledged: bool
        BlockerReleased: bool
        OutputDisposed: bool
        ErrorDisposed: bool
        ActiveAtVerdict: bool
        ElapsedMilliseconds: int64
    }

    type private CompletionWriter(inner: TextWriter, completed: ManualResetEventSlim) =
        inherit TextWriter()
        override _.Encoding = inner.Encoding

        override _.Write(buffer: char array, index: int, count: int) =
            inner.Write(buffer, index, count)

        override _.Dispose disposing =
            if disposing then
                try
                    inner.Dispose()
                finally
                    completed.Set()

    let private assertObservation observation =
        Expect.isTrue observation.BlockerAcknowledged "The sole blocker must acknowledge entry"
        Expect.equal observation.MaximumBeforeCapture 1 "Capture must start with maximum one worker"

        Expect.equal
            observation.AvailableBeforeCapture
            0
            "Capture must start with no available worker"

        Expect.equal observation.MaximumAtVerdict 1 "Verification must retain maximum one worker"
        Expect.equal observation.AvailableAtVerdict 0 "Verification must have no available worker"
        Expect.isFalse observation.BlockerReleased "Verification must precede blocker release"
        Expect.isTrue observation.ActiveAtVerdict "Verification must precede recovery"
        Expect.equal observation.CaptureError "" "Capture must complete without an exception"
        Expect.equal observation.CaptureExitCode 17 "Capture must preserve exit 17"

        Expect.equal
            observation.StandardOutput
            parentOutput
            "Captured stdout must preserve exact text"

        Expect.equal
            observation.StandardError
            parentError
            "Captured stderr must preserve exact text"

        Expect.equal
            observation.DurableOutput
            parentOutput
            "Durable stdout must be exact before release"

        Expect.equal
            observation.DurableError
            parentError
            "Durable stderr must be exact before release"

        Expect.isTrue observation.OutputDisposed "The stdout writer must finish before verification"
        Expect.isTrue observation.ErrorDisposed "The stderr writer must finish before verification"

    let internal runCaptureWorker request =
        let path name = Path.Combine(request.Directory, name)
        use blockerEntered = new ManualResetEventSlim()
        use blockerRelease = new ManualResetEventSlim()
        use blockerExited = new ManualResetEventSlim()
        use callerReady = new ManualResetEventSlim()
        use callerBegin = new ManualResetEventSlim()
        use readerSetup = new ManualResetEventSlim()
        use callerFinished = new ManualResetEventSlim()
        use outputDisposed = new ManualResetEventSlim()
        use errorDisposed = new ManualResetEventSlim()

        let stdout =
            new CompletionWriter(
                new StreamWriter(
                    path "stdout.txt",
                    false,
                    Text.UTF8Encoding(false),
                    AutoFlush = true
                ),
                outputDisposed
            )

        let stderr =
            try
                new CompletionWriter(
                    new StreamWriter(
                        path "stderr.txt",
                        false,
                        Text.UTF8Encoding(false),
                        AutoFlush = true
                    ),
                    errorDisposed
                )
            with _ ->
                stdout.Dispose()
                reraise ()

        let start =
            startInfo request.Directory [
                request.FixtureAssembly
                "gated"
                request.Directory
            ]

        let originalMinimum, originalIoMinimum = ThreadPool.GetMinThreads()
        let originalMaximum, originalIoMaximum = ThreadPool.GetMaxThreads()
        let elapsed = Stopwatch()

        let remaining () =
            max
                0
                (8000
                 - int elapsed.ElapsedMilliseconds)

        let mutable phase = int WorkerPhase.Active
        let mutable observation: WorkerObservation option = None
        let mutable callerFailure: exn option = None
        let mutable supervisorFailure: exn option = None
        let mutable captureBegun = false
        let mutable blockerQueued = false

        let caller =
            Thread(
                ThreadStart(fun () ->
                    callerReady.Set()
                    callerBegin.Wait()

                    try
                        if Volatile.Read(&phase) = int WorkerPhase.Active then
                            let maximum, _ = ThreadPool.GetMaxThreads()
                            let available, _ = ThreadPool.GetAvailableThreads()

                            Expect.isTrue
                                blockerEntered.IsSet
                                "The blocker must enter before capture"

                            Expect.isFalse blockerRelease.IsSet "The blocker must remain occupied"
                            Expect.equal maximum 1 "Capture requires maximum one worker"
                            Expect.equal available 0 "Capture requires no available worker"
                            captureBegun <- true

                            let captured =
                                attempt (fun () ->
                                    ProcessCapture.runWithOutputAfterReaderSetup
                                        readerSetup.Set
                                        stdout
                                        stderr
                                        (TimeSpan.FromSeconds 1.)
                                        start
                                )

                            let verdictMaximum, _ = ThreadPool.GetMaxThreads()
                            let verdictAvailable, _ = ThreadPool.GetAvailableThreads()

                            let current = {
                                CaptureExitCode =
                                    match captured with
                                    | Ok result -> result.ExitCode
                                    | Error _ -> -1
                                StandardOutput =
                                    match captured with
                                    | Ok result -> result.StandardOutput
                                    | Error _ -> ""
                                StandardError =
                                    match captured with
                                    | Ok result -> result.StandardError
                                    | Error _ -> ""
                                CaptureError =
                                    match captured with
                                    | Ok _ -> ""
                                    | Error ex -> ex.ToString()
                                DurableOutput = readLiveText (path "stdout.txt")
                                DurableError = readLiveText (path "stderr.txt")
                                MaximumBeforeCapture = maximum
                                AvailableBeforeCapture = available
                                MaximumAtVerdict = verdictMaximum
                                AvailableAtVerdict = verdictAvailable
                                BlockerAcknowledged = blockerEntered.IsSet
                                BlockerReleased = blockerRelease.IsSet
                                OutputDisposed = outputDisposed.IsSet
                                ErrorDisposed = errorDisposed.IsSet
                                ActiveAtVerdict = Volatile.Read(&phase) = int WorkerPhase.Active
                                ElapsedMilliseconds = elapsed.ElapsedMilliseconds
                            }

                            observation <- Some current
                            assertObservation current

                            Expect.isLessThan
                                elapsed.ElapsedMilliseconds
                                8000L
                                "Verification must meet the experiment deadline"

                            Expect.equal
                                (Interlocked.CompareExchange(
                                    &phase,
                                    int WorkerPhase.Verified,
                                    int WorkerPhase.Active
                                ))
                                (int WorkerPhase.Active)
                                "Verification must claim success before recovery"
                    with ex ->
                        callerFailure <- Some ex

                    callerFinished.Set()
                )
            )

        caller.IsBackground <- true
        caller.Start()

        try
            Expect.isTrue
                (callerReady.Wait(TimeSpan.FromSeconds 5.))
                "The dedicated caller must become ready"

            File.WriteAllText(path "worker-ready", string Environment.ProcessId)
            elapsed.Start()

            Expect.isTrue
                (ThreadPool.SetMinThreads(1, originalIoMinimum))
                "The worker minimum must be set"

            Expect.isTrue
                (ThreadPool.SetMaxThreads(1, originalIoMaximum))
                "The worker maximum must be set"

            Expect.isTrue
                (SpinWait.SpinUntil(
                    (fun () -> let available, _ = ThreadPool.GetAvailableThreads() in available = 1),
                    remaining ()
                ))
                "The child pool must become idle before the blocker starts"

            blockerQueued <-
                ThreadPool.QueueUserWorkItem(
                    WaitCallback(fun _ ->
                        blockerEntered.Set()
                        blockerRelease.Wait()
                        blockerExited.Set()
                    )
                )

            Expect.isTrue blockerQueued "The sole blocker must be queued"

            Expect.isTrue
                (blockerEntered.Wait(remaining ()))
                "The sole blocker must acknowledge entry"

            let maximum, _ = ThreadPool.GetMaxThreads()
            let available, _ = ThreadPool.GetAvailableThreads()
            Expect.equal maximum 1 "The child pool must have maximum one worker"
            Expect.equal available 0 "The acknowledged blocker must occupy the sole worker"
            callerBegin.Set()

            Expect.isTrue
                (readerSetup.Wait(remaining ()))
                "Both readers and their observer must exist before emission"

            Expect.isTrue
                (SpinWait.SpinUntil((fun () -> File.Exists(path "fixture-ready")), remaining ()))
                "The gated fixture must acknowledge readiness"

            let available, _ = ThreadPool.GetAvailableThreads()
            Expect.equal available 0 "The blocker must remain occupied before emission"
            Expect.isFalse blockerRelease.IsSet "Emission must precede blocker release"
            File.WriteAllText(path "emit", "emit")

            Expect.isTrue
                (callerFinished.Wait(remaining ()))
                "Capture and verification must meet the fixed eight-second deadline"
        with ex ->
            supervisorFailure <- Some ex

        let deadlineRecovery =
            elapsed.ElapsedMilliseconds
            >= 8000L

        let claimed =
            Interlocked.CompareExchange(&phase, int WorkerPhase.Recovering, int WorkerPhase.Active)

        let releaseMilliseconds = elapsed.ElapsedMilliseconds

        try
            File.WriteAllText(path "emit", "emit")
        with ex ->
            if supervisorFailure.IsNone then
                supervisorFailure <- Some ex

        blockerRelease.Set()
        let maximumRestored = ThreadPool.SetMaxThreads(originalMaximum, originalIoMaximum)
        let minimumRestored = ThreadPool.SetMinThreads(originalMinimum, originalIoMinimum)
        callerBegin.Set()
        caller.Join()

        if blockerQueued then
            blockerExited.Wait()

        if not captureBegun then
            stdout.Dispose()
            stderr.Dispose()

        outputDisposed.Wait()
        errorDisposed.Wait()
        let restoredMaximum, restoredIoMaximum = ThreadPool.GetMaxThreads()
        let restoredMinimum, restoredIoMinimum = ThreadPool.GetMinThreads()

        let restored =
            maximumRestored
            && minimumRestored
            && restoredMaximum = originalMaximum
            && restoredIoMaximum = originalIoMaximum
            && restoredMinimum = originalMinimum
            && restoredIoMinimum = originalIoMinimum

        let verified =
            claimed = int WorkerPhase.Verified
            && not deadlineRecovery
            && callerFailure.IsNone
            && supervisorFailure.IsNone
            && restored

        let report = {|
            Verified = verified
            DeadlineRecovery = deadlineRecovery
            Phase = string (enum<WorkerPhase> (Volatile.Read(&phase)))
            Observation =
                observation
                |> Option.map (fun frozen -> {|
                    CaptureExitCode = frozen.CaptureExitCode
                    StandardOutput = frozen.StandardOutput
                    StandardError = frozen.StandardError
                    CaptureError = frozen.CaptureError
                    DurableOutput = frozen.DurableOutput
                    DurableError = frozen.DurableError
                    MaximumBeforeCapture = frozen.MaximumBeforeCapture
                    AvailableBeforeCapture = frozen.AvailableBeforeCapture
                    MaximumAtVerdict = frozen.MaximumAtVerdict
                    AvailableAtVerdict = frozen.AvailableAtVerdict
                    BlockerAcknowledged = frozen.BlockerAcknowledged
                    BlockerReleased = frozen.BlockerReleased
                    OutputDisposed = frozen.OutputDisposed
                    ErrorDisposed = frozen.ErrorDisposed
                    ActiveAtVerdict = frozen.ActiveAtVerdict
                    ElapsedMilliseconds = frozen.ElapsedMilliseconds
                |})
                |> Option.toArray
            CallerFailure =
                callerFailure
                |> Option.map string
                |> Option.defaultValue ""
            SupervisorFailure =
                supervisorFailure
                |> Option.map string
                |> Option.defaultValue ""
            ReleaseMilliseconds = releaseMilliseconds
            Restored = restored
            BlockerExited =
                not blockerQueued
                || blockerExited.IsSet
            OutputDisposed = outputDisposed.IsSet
            ErrorDisposed = errorDisposed.IsSet
            LaterOutput = readLiveText (path "stdout.txt")
            LaterError = readLiveText (path "stderr.txt")
            ElapsedMilliseconds = elapsed.ElapsedMilliseconds
        |}

        File.WriteAllText(path "receipt.json", Text.Json.JsonSerializer.Serialize report)
        if verified then 0 else 1

    let private captureWithoutPoolWorkers () =
        let assembly = fixture.Value
        let directory = diagnosticsDirectory ()

        Directory.CreateDirectory directory
        |> ignore

        let start =
            startInfo Environment.CurrentDirectory [
                typeof<WorkerDispatch>.Assembly.Location
                workerArgument
                assembly
                directory
            ]

        start.FileName <-
            Path.GetFullPath(
                Path.Combine(
                    Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(),
                    "../../..",
                    if OperatingSystem.IsWindows() then
                        "dotnet.exe"
                    else
                        "dotnet"
                )
            )

        start.RedirectStandardOutput <- false
        start.RedirectStandardError <- false
        start.StandardOutputEncoding <- null
        start.StandardErrorEncoding <- null
        use worker = new Process(StartInfo = start)
        Expect.isTrue (worker.Start()) "The isolated regression worker must start"
        let ready = Path.Combine(directory, "worker-ready")
        let preparation = Stopwatch.StartNew()

        while not worker.HasExited
              && not (File.Exists ready)
              && preparation.Elapsed < TimeSpan.FromSeconds 30. do
            worker.WaitForExit(10)
            |> ignore

        let readySeen = File.Exists ready

        let exited =
            worker.HasExited
            || (readySeen
                && worker.WaitForExit(12000))

        if not exited then
            File.WriteAllText(
                Path.Combine(directory, "parent-timeout.txt"),
                "The isolated worker exceeded its preparation or experiment/cleanup bound."
            )

            worker.Kill(true)
            worker.WaitForExit()

        let receiptPath = Path.Combine(directory, "receipt.json")

        let receipt =
            if File.Exists receiptPath then
                File.ReadAllText receiptPath
            else
                "No worker receipt."

        let detail = sprintf "Worker evidence: %s\n%s" directory receipt
        Expect.isTrue exited detail
        Expect.isTrue readySeen detail
        Expect.equal worker.ExitCode 0 detail
        use document = Text.Json.JsonDocument.Parse receipt
        let root = document.RootElement
        Expect.isTrue (root.GetProperty("Verified").GetBoolean()) detail
        Expect.isFalse (root.GetProperty("DeadlineRecovery").GetBoolean()) detail
        Expect.isTrue (root.GetProperty("Restored").GetBoolean()) detail
        Expect.equal (root.GetProperty("Phase").GetString()) "Verified" detail

        let frozen =
            root.GetProperty("Observation").EnumerateArray()
            |> Seq.exactlyOne

        Expect.equal (frozen.GetProperty("CaptureError").GetString()) "" detail
        Expect.equal (frozen.GetProperty("CaptureExitCode").GetInt32()) 17 detail

        for property, expected in
            [
                "StandardOutput", parentOutput
                "StandardError", parentError
                "DurableOutput", parentOutput
                "DurableError", parentError
            ] do
            Expect.equal (frozen.GetProperty(property).GetString()) expected detail

        for property in
            [
                "MaximumBeforeCapture"
                "MaximumAtVerdict"
            ] do
            Expect.equal (frozen.GetProperty(property).GetInt32()) 1 detail

        for property in
            [
                "AvailableBeforeCapture"
                "AvailableAtVerdict"
            ] do
            Expect.equal (frozen.GetProperty(property).GetInt32()) 0 detail

        for property in
            [
                "BlockerAcknowledged"
                "OutputDisposed"
                "ErrorDisposed"
                "ActiveAtVerdict"
            ] do
            Expect.isTrue (frozen.GetProperty(property).GetBoolean()) detail

        Expect.isFalse (frozen.GetProperty("BlockerReleased").GetBoolean()) detail

    type private FaultingWriter(writeError: exn, disposeError: exn) =
        inherit TextWriter()
        let mutable writes = 0
        let mutable disposals = 0
        member _.Writes = writes
        member _.Disposals = disposals
        override _.Encoding = Text.Encoding.UTF8

        override _.Write(_: char array, _: int, _: int) : unit =
            writes <- writes + 1
            raise writeError

        override _.Dispose disposing =
            if disposing then
                disposals <-
                    disposals
                    + 1

                raise disposeError

    [<Tests>]
    let tests =
        testList "process capture" [
            testCase "captures exact output with no available pool workers"
            <| fun _ -> captureWithoutPoolWorkers ()

            testCase "continues capture after writer faults and disposes each writer once"
            <| fun _ ->
                use directory = Disposables.DisposableDirectory.Create()
                let outputWrite = IOException "stdout write failure"
                let outputDispose = IOException "stdout disposal failure"
                let errorWrite = IOException "stderr write failure"
                let errorDispose = IOException "stderr disposal failure"
                let stdout = new FaultingWriter(outputWrite, outputDispose)
                let stderr = new FaultingWriter(errorWrite, errorDispose)

                let error =
                    attempt (fun () ->
                        fixtureStart directory.Directory [ "exact" ]
                        |> ProcessCapture.runWithOutput stdout stderr (TimeSpan.FromSeconds 1.)
                    )
                    |> failure

                Expect.isTrue
                    (error :? InvalidOperationException)
                    "Writer faults must retain their context wrapper"

                Expect.stringContains
                    error.Message
                    exactOutput
                    "Capture must continue after the stdout writer fails"

                Expect.stringContains
                    error.Message
                    exactError
                    "Capture must continue after the stderr writer fails"

                let causes = (error.InnerException :?> AggregateException).Flatten().InnerExceptions

                Expect.equal
                    causes.Count
                    4
                    "Both write and disposal failures from both streams must survive"

                for cause in
                    [
                        outputWrite
                        outputDispose
                        errorWrite
                        errorDispose
                    ] do
                    Expect.isTrue
                        (causes
                         |> Seq.exists (fun actual -> Object.ReferenceEquals(actual, cause)))
                        "The original writer exception must survive"

                for writer in
                    [
                        stdout
                        stderr
                    ] do
                    Expect.equal writer.Writes 1 "A failed writer must not receive later blocks"

                    Expect.equal
                        writer.Disposals
                        1
                        "Each launched reader must dispose its writer once"

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
                    Task.Factory.StartNew(
                        (fun () ->
                            attempt (fun () -> ProcessCapture.run (TimeSpan.FromSeconds 1.) start)
                        ),
                        CancellationToken.None,
                        TaskCreationOptions.LongRunning,
                        TaskScheduler.Default
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
                    Task.Factory.StartNew(
                        (fun () ->
                            attempt (fun () ->
                                Builds.executeBuildWithDiagnostics
                                    diagnostics
                                    (TimeSpan.FromSeconds 1.)
                                    directory.Directory
                                    "ControlledDrain"
                            )
                        ),
                        CancellationToken.None,
                        TaskCreationOptions.LongRunning,
                        TaskScheduler.Default
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
