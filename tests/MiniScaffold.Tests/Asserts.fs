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
        let filepath = Path.Combine(d.FullName, file)
        if  filepath |> File.Exists |> not then
            failtestf "Could not find %s" filepath

    let ``project can build target`` target (d : DirectoryInfo) =
        Builds.executeBuild d.FullName target

    let ``CHANGELOG exists`` =
        tryFindFile "CHANGELOG.md"

    let ``.config/dotnet-tools.json exists`` =
        tryFindFile ".config/dotnet-tools.json"

    let ``.github ISSUE_TEMPLATE bug_report exists`` =
        tryFindFile ".github/ISSUE_TEMPLATE/bug_report.md"

    let ``.github ISSUE_TEMPLATE feature_request exists`` =
        tryFindFile ".github/ISSUE_TEMPLATE/feature_request.md"

    let ``.github workflows build exists`` =
        tryFindFile ".github/workflows/build.yml"

    let ``.github ISSUE_TEMPLATE exists`` =
        tryFindFile ".github/ISSUE_TEMPLATE.md"

    let ``.github PULL_REQUEST_TEMPLATE exists`` =
        tryFindFile ".github/PULL_REQUEST_TEMPLATE.md"

    let ``.editorconfig exists`` =
        tryFindFile ".editorconfig"

    let ``.gitattributes exists`` =
        tryFindFile ".gitattributes"

    let ``.gitignore exists`` =
        tryFindFile ".gitignore"

    let ``LICENSE exists`` =
        tryFindFile "LICENSE.md"

    let ``paket.lock exists`` =
        tryFindFile "paket.lock"

    let ``paket.dependencies exists`` =
        tryFindFile "paket.dependencies"

    let ``README exists`` =
        tryFindFile "README.md"
