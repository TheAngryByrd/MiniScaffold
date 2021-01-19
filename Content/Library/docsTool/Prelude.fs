namespace DocsTool

module Uri =
    open System
    let simpleCombine (slug : string) (baseUri : Uri) =
        sprintf "%s/%s" (baseUri.AbsoluteUri.TrimEnd('/')) (slug.TrimStart('/'))

    let create (url : string) =
        match Uri.TryCreate(url, UriKind.Absolute) with
        | (true, v) -> v
        | _ -> failwithf "Bad url %s" url



module Diposeable =
    open System
    open Fake.Core
    let dispose (d : #IDisposable) = d.Dispose()

    type DisposableList =
        {
            Disposables : IDisposable list
        } interface IDisposable with
            member x.Dispose () =
                x.Disposables |> List.iter(dispose)
          static member Create(disposables) =
            {
                Disposables = disposables
            } :> IDisposable

    type DisposableDirectory (directory : string) =
        do
            Trace.tracefn "Created disposable directory %s" directory
        static member Create() =
            let tempPath = IO.Path.Combine(IO.Path.GetTempPath(), Guid.NewGuid().ToString("n"))
            IO.Directory.CreateDirectory tempPath |> ignore

            new DisposableDirectory(tempPath)
        member x.Directory = directory
        member x.DirectoryInfo = IO.DirectoryInfo(directory)

        interface IDisposable with
            member x.Dispose() =
                Trace.tracefn "Deleting directory %s" directory
                IO.Directory.Delete(x.Directory,true)
