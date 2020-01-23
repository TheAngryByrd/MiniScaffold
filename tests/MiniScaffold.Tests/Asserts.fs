namespace MiniScaffold.Tests
open System.IO
open Expecto
open Infrastructure

module Assert =
    open System


    let private failIfNoneWithMsg msg opt =
        match opt with
        | Some _ -> ()
        | None -> failtest msg

    let private tryFindFile file (d : DirectoryInfo) =
        d.GetFiles()
        |> Seq.tryFind(fun x -> x.Name = file)
        |> failIfNoneWithMsg (sprintf "Could not find %s in %s" file d.FullName)

    let ``project can build target`` target (d : DirectoryInfo) =
        Builds.executeBuild d.FullName target

    let ``.editorconfig exists`` (d : DirectoryInfo) =
        tryFindFile ".editorconfig" d

    let ``.gitattributes exists`` (d : DirectoryInfo) =
        tryFindFile ".gitattributes" d

    let ``paket.lock exists`` (d : DirectoryInfo) =
        tryFindFile "paket.lock" d

    let ``paket.dependencies exists`` (d : DirectoryInfo) =
        tryFindFile "paket.dependencies" d
