namespace MiniScaffold.Tests
open System.IO
open Expecto

module Assert =
    open System
    type Foo = {
        name : string
    }
        interface IDisposable with
            member x.Disposable() = ()


    let private failIfNoneWithMsg msg opt =
        match opt with
        | Some _ -> ()
        | None -> failtest msg

    let private tryFindFile file (d : DirectoryInfo) =
        d.GetFiles()
        |> Seq.tryFind(fun x -> x.Name = file)
        |> failIfNoneWithMsg (sprintf "Could not find %s in %s" file d.FullName)

    let ``.editorconfig exists`` (d : DirectoryInfo) =
        tryFindFile ".editorconfig" d

    let ``.gitattributes exists`` (d : DirectoryInfo) =
        tryFindFile ".gitattributes" d

    let ``paket.lock exists`` (d : DirectoryInfo) =
        tryFindFile "paket.lock" d

    let ``paket.dependencies exists`` (d : DirectoryInfo) =
        tryFindFile "paket.dependencies" d
