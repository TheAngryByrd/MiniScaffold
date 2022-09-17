namespace Infrastructure


module Dotnet =
    open Fake.Core
    open Fake.DotNet

    let failOnBadExitAndPrint (p: ProcessResult) =
        if
            p.ExitCode
            <> 0
        then
            p.Errors
            |> Seq.iter Trace.traceError

            failwithf "failed with exitcode %d" p.ExitCode


    module New =
        let cmd (opt: DotNet.Options -> DotNet.Options) args =
            printfn "dotnet new %s" args
            // let args = args |> String.concat " "
            DotNet.exec opt "new" args
            |> failOnBadExitAndPrint

        let install name =
            let args =
                Arguments.Empty
                |> Arguments.appendNotEmpty "-i" name
            // |> Arguments.appendRaw "--dev:install"
            //     |>
            // let args = [
            //     sprintf "-i \"%s\"" name
            // ]
            cmd id args.ToStartInfo

        let uninstall name =
            let args =
                Arguments.Empty
                |> Arguments.appendNotEmpty "-u" name

            cmd id args.ToStartInfo


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

module Builds =
    open Fake.Core

    let executeBuild workingDir testTarget =
        let cmd, args =
            if Environment.isUnix then
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
        // printfn "running %s" cmd
        let result =
            CreateProcess.fromRawCommand cmd args
            |> CreateProcess.withWorkingDirectory workingDir
            |> CreateProcess.ensureExitCode
            |> Proc.run

        ()
