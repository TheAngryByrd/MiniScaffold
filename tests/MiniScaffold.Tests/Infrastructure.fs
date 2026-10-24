namespace Infrastructure


module Disposables =
    open System

    let dispose (disposable: #IDisposable) = disposable.Dispose()

    [<AllowNullLiteral>]
    type DisposableDirectory(directory: string) =

        static member Create() =
            let tempPath =
                IO.Path.Combine(
                    IO.Path.GetTempPath(),
                    IO.Path.GetFileNameWithoutExtension(IO.Path.GetTempFileName())
                )

            IO.Directory.CreateDirectory tempPath
            |> ignore

            new DisposableDirectory(tempPath)

        member x.Directory = directory
        member x.DirectoryInfo = IO.DirectoryInfo(directory)

        interface IDisposable with
            member x.Dispose() =
                // Git objects are created read-only, so on Windows we have to mark them read-write before deleting them
                x.DirectoryInfo.EnumerateFiles("*", IO.SearchOption.AllDirectories)
                |> Seq.iter (fun fileInfo ->
                    if fileInfo.IsReadOnly then
                        fileInfo.IsReadOnly <- false
                )

                IO.Directory.Delete(x.Directory, true)

module Dotnet =
    open Fake.Core
    open Fake.DotNet
    open System.Threading

    let private fantomasRetryDelayMs = 250
    let private fantomasMaxRetries = 2

    let failOnBadExitAndPrint (p: ProcessResult) =
        if
            p.ExitCode
            <> 0
        then
            p.Errors
            |> Seq.iter Trace.traceError

            failwithf "failed with exitcode %d" p.ExitCode

    let fantomas workingDir args =
        let rec run attempts =
            let result =
                DotNet.exec
                    (fun opt -> {
                        opt with
                            WorkingDirectory = workingDir
                    })
                    "fantomas"
                    args

            if
                // Exit code 137 can occur when Fantomas is transiently terminated by the OS.
                result.ExitCode = 137
                && attempts > 0
            then
                Thread.Sleep fantomasRetryDelayMs

                run (
                    attempts
                    - 1
                )
            else
                result
                |> failOnBadExitAndPrint

        run fantomasMaxRetries

    module New =
        let private hive = lazy (Disposables.DisposableDirectory.Create())

        do
            System.AppDomain.CurrentDomain.ProcessExit.Add(fun _ ->
                if hive.IsValueCreated then
                    Disposables.dispose hive.Value
            )

        let cmd (opt: DotNet.Options -> DotNet.Options) args =
            let args =
                Arguments.OfStartInfo args
                |> Arguments.appendNotEmpty "--debug:custom-hive" hive.Value.Directory

            DotNet.exec opt "new" args.ToStartInfo
            |> failOnBadExitAndPrint

        let install name =
            let args =
                Arguments.Empty
                |> Arguments.appendNotEmpty "install" name
                |> Arguments.appendRaw "--force"

            cmd id args.ToStartInfo


module internal ProcessCapture =
    open System
    open System.Diagnostics
    open System.IO
    open System.Text
    open System.Threading.Tasks

    type ProcessOutput = {
        ExitCode: int
        StandardOutput: string
        StandardError: string
    }

    let describe (startInfo: ProcessStartInfo) =
        let arguments =
            if startInfo.ArgumentList.Count = 0 then
                startInfo.Arguments
            else
                startInfo.ArgumentList
                |> Seq.map (sprintf "%A")
                |> String.concat " "

        sprintf "%s %s in %s" startInfo.FileName arguments startInfo.WorkingDirectory

    let private readStream (reader: StreamReader) (writer: TextWriter) (captured: StringBuilder) =
        Task.Factory.StartNew<unit>(
            (fun () ->
                let failures = ResizeArray<exn>()
                let buffer = Array.zeroCreate<char> 1
                let mutable writeOutput = true

                try
                    let mutable reading = true

                    while reading do
                        let count = reader.Read(buffer, 0, buffer.Length)
                        reading <- count <> 0

                        if reading then
                            lock
                                captured
                                (fun () ->
                                    captured.Append(buffer, 0, count)
                                    |> ignore
                                )

                            if writeOutput then
                                try
                                    writer.Write(buffer, 0, count)
                                with ex ->
                                    failures.Add ex
                                    writeOutput <- false
                with ex ->
                    failures.Add ex

                try
                    writer.Dispose()
                with ex ->
                    failures.Add ex

                if failures.Count > 0 then
                    raise (AggregateException(failures))
            ),
            System.Threading.CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default
        )

    let runWithOutputAfterReaderSetup
        (onReaderSetup: unit -> unit)
        (stdout: TextWriter)
        (stderr: TextWriter)
        (drainTimeout: TimeSpan)
        (startInfo: ProcessStartInfo)
        =
        use child = new Process(StartInfo = startInfo)

        try
            child.Start()
            |> ignore
        with _ ->
            for writer in
                [
                    stdout
                    stderr
                ] do
                try
                    writer.Dispose()
                with _ ->
                    ()

            reraise ()

        let output = StringBuilder()
        let errors = StringBuilder()

        let outputRead =
            try
                readStream child.StandardOutput stdout output
            with _ ->
                for writer in
                    [
                        stdout
                        stderr
                    ] do
                    try
                        writer.Dispose()
                    with _ ->
                        ()

                reraise ()

        let errorRead =
            try
                readStream child.StandardError stderr errors
            with _ ->
                outputRead.ContinueWith(
                    (fun (completed: Task<unit>) ->
                        if completed.IsFaulted then
                            completed.Exception
                            |> ignore
                    ),
                    TaskScheduler.Default
                )
                |> ignore

                try
                    stderr.Dispose()
                with _ ->
                    ()

                reraise ()

        let outputDrain =
            Task.WhenAll [|
                outputRead
                errorRead
            |]

        outputDrain.ContinueWith(
            (fun (completed: Task<unit array>) ->
                if completed.IsFaulted then
                    completed.Exception
                    |> ignore
            ),
            TaskScheduler.Default
        )
        |> ignore

        onReaderSetup ()
        child.WaitForExit()
        let context = sprintf "%s exited with code %d" (describe startInfo) child.ExitCode

        let snapshot (captured: StringBuilder) =
            lock captured (fun () -> captured.ToString())

        let failureMessage reason =
            sprintf
                "%s, %s\nstdout:\n%s\nstderr:\n%s"
                context
                reason
                (snapshot output)
                (snapshot errors)

        let drained =
            try
                (outputDrain :> Task).Wait drainTimeout
            with ex ->
                raise (
                    InvalidOperationException(
                        failureMessage (sprintf "but output draining failed: %s" ex.Message),
                        ex
                    )
                )

        if not drained then
            raise (
                TimeoutException(
                    failureMessage (
                        sprintf
                            "but stdout/stderr did not close within %g seconds after exit."
                            drainTimeout.TotalSeconds
                    )
                )
            )

        {
            ExitCode = child.ExitCode
            StandardOutput = snapshot output
            StandardError = snapshot errors
        }

    let runWithOutput stdout stderr drainTimeout startInfo =
        runWithOutputAfterReaderSetup ignore stdout stderr drainTimeout startInfo

    let run drainTimeout startInfo =
        runWithOutput TextWriter.Null TextWriter.Null drainTimeout startInfo

module Builds =
    open System
    open System.Diagnostics
    open System.IO
    open System.Text

    let internal diagnosticsRoot =
        Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "../../TestResults/generated-builds"))

    let private retainFailure destination workingDir diagnostics =
        try
            Directory.CreateDirectory destination
            |> ignore

            File.WriteAllText(Path.Combine(destination, "process.txt"), diagnostics)

            for file in Directory.EnumerateFiles(workingDir, "*.trx", SearchOption.AllDirectories) do
                let relative = Path.GetRelativePath(workingDir, file)

                if
                    relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    |> Array.contains "TestResults"
                then
                    let retained = Path.Combine(destination, relative)

                    Directory.CreateDirectory(Path.GetDirectoryName retained)
                    |> ignore

                    File.Copy(file, retained)

            sprintf "Diagnostics retained in %s" destination
        with ex ->
            sprintf "Could not retain all diagnostics in %s: %s" destination ex.Message

    let private executeProcess destination drainTimeout (startInfo: ProcessStartInfo) =
        let recordingFailures = ResizeArray<string>()

        try
            Directory.CreateDirectory destination
            |> ignore

            File.WriteAllText(
                Path.Combine(destination, "command.txt"),
                ProcessCapture.describe startInfo
            )
        with ex ->
            recordingFailures.Add ex.Message

        let writer name =
            try
                new StreamWriter(
                    Path.Combine(destination, name),
                    false,
                    UTF8Encoding(false),
                    AutoFlush = true
                )
                :> TextWriter
            with ex ->
                recordingFailures.Add ex.Message
                TextWriter.Null

        let stdout = writer "stdout.txt"
        let stderr = writer "stderr.txt"

        let result =
            try
                ProcessCapture.runWithOutput stdout stderr drainTimeout startInfo
            with ex ->
                let retained = retainFailure destination startInfo.WorkingDirectory (ex.ToString())
                raise (InvalidOperationException(sprintf "%s\n%s" ex.Message retained, ex))

        let diagnostics =
            sprintf
                "%s exited with code %d.\nstdout:\n%s\nstderr:\n%s"
                (ProcessCapture.describe startInfo)
                result.ExitCode
                result.StandardOutput
                result.StandardError

        if
            result.ExitCode
            <> 0
            || recordingFailures.Count > 0
        then
            let diagnostics =
                if recordingFailures.Count = 0 then
                    diagnostics
                else
                    sprintf
                        "%s\nOutput recording failed: %s"
                        diagnostics
                        (String.concat "\n" recordingFailures)

            let retained = retainFailure destination startInfo.WorkingDirectory diagnostics
            invalidOp (sprintf "%s\n%s" diagnostics retained)
        else
            File.WriteAllText(Path.Combine(destination, "process.txt"), diagnostics)

    let internal executeBuildWithDiagnostics destination drainTimeout workingDir testTarget =
        let cmd, args =
            if not (OperatingSystem.IsWindows()) then
                "bash",
                [
                    sprintf "./build.sh"
                    testTarget
                ]
            else
                "cmd.exe",
                [
                    "/c"
                    ".\\build.cmd"
                    testTarget
                ]

        let startInfo =
            ProcessStartInfo(
                cmd,
                WorkingDirectory = workingDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            )

        args
        |> List.iter startInfo.ArgumentList.Add

        startInfo.Environment.Remove "MSBuildSDKsPath"
        |> ignore

        startInfo.Environment.Remove "MSBuildExtensionsPath"
        |> ignore

        startInfo.Environment["MSBUILDDISABLENODEREUSE"] <- "1"
        executeProcess destination drainTimeout startInfo

    let executeBuild workingDir testTarget =
        let destination = Path.Combine(diagnosticsRoot, Guid.NewGuid().ToString("N"))
        executeBuildWithDiagnostics destination (TimeSpan.FromSeconds 30.) workingDir testTarget
