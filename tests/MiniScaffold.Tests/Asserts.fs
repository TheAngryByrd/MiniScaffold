namespace MiniScaffold.Tests

open System.IO
open System.IO.Compression
open System.Diagnostics
open System.Reflection
open System.Reflection.Metadata
open System.Runtime.Loader
open System.Xml.Linq
open Expecto
open Infrastructure
open Fake.IO.FileSystemOperators

module Array =
    let insert v i (l: 'a array) =
        let newArray =
            Array.zeroCreate (
                l.Length
                + 1
            )

        let mutable newPointer = 0

        l
        |> Array.iteri (fun j y ->
            if i = j then
                newArray.[newPointer] <- v

                newPointer <-
                    newPointer
                    + 1

                newArray.[newPointer] <- y

                newPointer <-
                    newPointer
                    + 1
            else
                newArray.[newPointer] <- y

                newPointer <-
                    newPointer
                    + 1
        )

        newArray

module Assert =
    let private deleteRetryDelayMs = 100
    let private maxDeleteRetries = 5

    open System

    let private failIfNoneWithMsg msg opt =
        match opt with
        | Some _ -> ()
        | None -> failtest msg

    let private tryFindFile file (d: DirectoryInfo) =
        let filepath = Path.Combine(d.FullName, file)

        if
            filepath
            |> File.Exists
            |> not
        then
            let message =
                sprintf "Could not find %s, all files currently in folder are:" filepath

            let message =
                (message, d.EnumerateFiles())
                ||> Seq.fold (fun state next ->
                    sprintf "%s%s%s" state Environment.NewLine next.FullName
                )

            failtest message

    let ``project can build target`` target (d: DirectoryInfo) =
        Builds.executeBuild d.FullName target

    let ``build target with failure expected`` target (d: DirectoryInfo) =
        let failed =
            try
                Builds.executeBuild d.FullName target
                false
            with _ ->
                true

        Expect.isTrue
            failed
            (sprintf "Building target %s succeeded when it should have failed" target)

    let ``CHANGELOG exists`` = tryFindFile "CHANGELOG.md"

    let ``CHANGELOG contains Unreleased section`` (d: DirectoryInfo) =
        let changelogContents =
            Path.Combine(d.FullName, "CHANGELOG.md")
            |> File.ReadAllLines

        Expect.contains
            changelogContents
            "## [Unreleased]"
            "Changelog should contain Unreleased section"

    let ``CHANGELOG does not contain Unreleased section`` (d: DirectoryInfo) =
        let changelogContents =
            Path.Combine(d.FullName, "CHANGELOG.md")
            |> File.ReadAllLines

        Expect.isFalse
            (changelogContents
             |> Array.contains "## [Unreleased]")
            "Changelog should not contain Unreleased section"

    let ``CHANGELOG links compare v-prefixed release tags``
        githubUsername
        repositoryName
        previousVersion
        newVersion
        (d: DirectoryInfo)
        =
        let repositoryUrl = $"https://github.com/{githubUsername}/{repositoryName}/"

        let actual =
            Path.Combine(d.FullName, "CHANGELOG.md")
            |> File.ReadAllLines
            |> Array.filter (fun line ->
                line.StartsWith "[Unreleased]:"
                || line.StartsWith $"[{newVersion}]:"
            )
            |> Array.toList

        Expect.equal
            actual
            [
                $"[Unreleased]: {repositoryUrl}/compare/v{newVersion}...HEAD"
                $"[{newVersion}]: {repositoryUrl}/compare/v{previousVersion}...v{newVersion}"
            ]
            "CHANGELOG link references should compare the v-prefixed tags that GitRelease pushes"

    let ``.config/dotnet-tools.json exists`` = tryFindFile ".config/dotnet-tools.json"

    let ``.github ISSUE_TEMPLATE bug_report exists`` =
        tryFindFile ".github/ISSUE_TEMPLATE/bug_report.md"

    let ``.github ISSUE_TEMPLATE feature_request exists`` =
        tryFindFile ".github/ISSUE_TEMPLATE/feature_request.md"

    let ``.github workflows build exists`` = tryFindFile ".github/workflows/build.yml"

    let ``.github ISSUE_TEMPLATE exists`` = tryFindFile ".github/ISSUE_TEMPLATE.md"

    let ``.github PULL_REQUEST_TEMPLATE exists`` =
        tryFindFile ".github/PULL_REQUEST_TEMPLATE.md"

    let ``.editorconfig exists`` = tryFindFile ".editorconfig"

    let ``.gitattributes exists`` = tryFindFile ".gitattributes"

    let ``.gitignore exists`` = tryFindFile ".gitignore"

    let ``LICENSE exists`` = tryFindFile "LICENSE.md"

    let ``NuGet.config exists`` = tryFindFile "NuGet.config"

    let ``Directory.Packages.props exists`` = tryFindFile "Directory.Packages.props"

    let ``README exists`` = tryFindFile "README.md"

    let ``File exists`` path = tryFindFile path

    let ``File does not exist`` file (d: DirectoryInfo) =
        let filepath = Path.Combine(d.FullName, file)
        Expect.isFalse (File.Exists filepath) (sprintf "%s should not exist" filepath)

    let ``docs have no unresolved substitutions`` (d: DirectoryInfo) =
        let docs = Path.Combine(d.FullName, "docs")

        // fsdocs leaves the edit link key unresolved on API pages (issue #366).
        let isKnownGap (page: string) placeholder =
            placeholder = "{{fsdocs-source-filename}}"
            && page.StartsWith "reference/"

        let pages =
            Directory.EnumerateFiles(docs, "*.html", SearchOption.AllDirectories)
            |> Seq.toList

        Expect.isNonEmpty pages $"{docs} should contain the generated documentation pages"

        let unresolved = [
            for file in pages do
                let page =
                    Path.GetRelativePath(docs, file).Replace(Path.DirectorySeparatorChar, '/')

                for placeholder in
                    System.Text.RegularExpressions.Regex.Matches(
                        File.ReadAllText file,
                        @"\{\{[a-z0-9-]+\}\}"
                    ) do
                    if not (isKnownGap page placeholder.Value) then
                        page, placeholder.Value
        ]

        Expect.isEmpty
            unresolved
            $"fsdocs should replace each template substitution: %A{unresolved}"

    let ``docs use the fsdocs search contract`` (d: DirectoryInfo) =
        let index = File.ReadAllText(Path.Combine(d.FullName, "docs", "index.html"))

        let matches (pattern: string) =
            System.Text.RegularExpressions.Regex.IsMatch(index, pattern)

        Expect.isTrue
            (matches """<html[^>]* data-root="[^"]+["]""")
            "fsdocs-search.js reads the site root from a non-empty data-root on <html>"

        Expect.isTrue
            (matches """<[a-z]+ id="search-btn["]""")
            "fsdocs-search.js opens the dialog from #search-btn"

        Expect.isTrue
            (matches """<script type="module" src="[^"]*content/fsdocs-search\.js["]""")
            "fsdocs-search.js is an ES module, so the page must load it with type=\"module\""

        let searchDialog = index.IndexOf "<dialog id=\"fsdocs-search\""
        Expect.isGreaterThanOrEqual searchDialog 0 "The page must contain the search dialog"

        Expect.equal
            (index.IndexOf "<dialog")
            searchDialog
            "fsdocs-search.js uses the first <dialog> of the page, so the search dialog must come first"

        let dialog =
            index.Substring(
                searchDialog,
                index.IndexOf("</dialog>", searchDialog)
                - searchDialog
            )

        for required in
            [
                "<input type=\"search\""
                "<ul"
                "class=\"empty\""
            ] do
            Expect.stringContains
                dialog
                required
                "fsdocs-search.js needs this element in the dialog"

    let private ``product module has visits`` (moduleName: string) (report: string) =
        Expect.isTrue (File.Exists report) $"AltCover should write {report}"

        let productModule =
            XDocument.Load(report).Descendants(XName.Get "Module")
            |> Seq.tryFind (fun m -> m.Element(XName.Get "ModuleName").Value = moduleName)

        match productModule with
        | None -> failtestf "%s has no module %s" report moduleName
        | Some m ->
            let visited =
                m.Element(XName.Get "Summary").Attribute(XName.Get "visitedSequencePoints").Value
                |> int

            Expect.isGreaterThan visited 0 $"The tests should visit {moduleName} ({report})"

    let private ``first source path`` (pdb: string) =
        use stream = File.OpenRead pdb
        use provider = MetadataReaderProvider.FromPortablePdbStream stream
        let reader = provider.GetMetadataReader()

        reader.Documents
        |> Seq.map (fun handle -> reader.GetString(reader.GetDocument(handle).Name))
        |> Seq.find _.EndsWith(".fs")

    // An incremental coverage build after a CI build without coverage kept the /_/ PDB paths that AltCoverLocalSource skips
    let private ``CI coverage after a CI build``
        projectName
        coverageEnvironment
        coverageTarget
        reports
        (d: DirectoryInfo)
        =
        let ci = [
            "CI", Some "true"
            "CONFIGURATION", Some "Debug"
        ]

        Builds.executeBuildWithEnvironment ci d.FullName "Clean"

        Builds.executeBuildWithEnvironment
            (ci
             @ [
                 "ENABLE_COVERAGE", None
                 "DISABLE_COVERAGE", Some "true"
             ])
            d.FullName
            "DotnetBuild"

        let pdb =
            Path.Combine(
                d.FullName,
                "src",
                projectName,
                "bin",
                "Debug",
                "net10.0",
                $"{projectName}.pdb"
            )

        Expect.stringStarts
            (``first source path`` pdb)
            "/_/"
            "A CI build without coverage should map the PDB source paths"

        for report in reports do
            File.Delete report

        Builds.executeBuildWithEnvironment
            (ci
             @ coverageEnvironment)
            d.FullName
            coverageTarget

        for report in reports do
            ``product module has visits`` projectName report

    let ``coverage report covers each target framework``
        testProjectName
        frameworks
        (d: DirectoryInfo)
        =
        let projectName = (testProjectName: string).Replace(".Tests", "")

        let reports = [
            for framework in frameworks ->
                Path.Combine(d.FullName, "tests", testProjectName, $"coverage.{framework}.xml")
        ]

        let coverage = [
            "ENABLE_COVERAGE", Some "true"
            "DISABLE_COVERAGE", None
        ]

        ``CI coverage after a CI build`` projectName coverage "DotnetTest" reports d

        for report in reports do
            File.Delete report

        Builds.executeBuildWithEnvironment
            [
                "CI", Some "true"
                "CONFIGURATION", Some "Debug"
                "ENABLE_COVERAGE", None
                "DISABLE_COVERAGE", None
            ]
            d.FullName
            "GenerateCoverageReport"

        for report in reports do
            ``product module has visits`` projectName report

        Expect.isTrue
            (File.Exists(Path.Combine(d.FullName, "docs", "coverage", "index.html")))
            "reportgenerator should write docs/coverage/index.html"

    let ``CI coverage report has visits`` testProjectName (d: DirectoryInfo) =
        let projectName = (testProjectName: string).Replace(".Tests", "")

        ``CI coverage after a CI build``
            projectName
            [ "DISABLE_COVERAGE", None ]
            "DotnetTest"
            [ Path.Combine(d.FullName, "tests", testProjectName, "coverage.xml") ]
            d

    let ``CI workflow collects coverage only in Debug`` (d: DirectoryInfo) =
        let expected = "ENABLE_COVERAGE: ${{ matrix.configuration == 'Debug' }}"

        let steps =
            File.ReadAllLines(Path.Combine(d.FullName, ".github", "workflows", "build.yml"))
            |> Array.fold
                (fun steps line ->
                    match steps with
                    | _ when line.StartsWith("      - ") ->
                        [ line.Trim() ]
                        :: steps
                    | step :: rest ->
                        (line.Trim()
                         :: step)
                        :: rest
                    | [] -> []
                )
                []
            |> List.rev
            |> List.map List.rev

        let coverageByStep =
            steps
            |> List.choose (fun step ->
                match
                    step
                    |> List.filter _.StartsWith("ENABLE_COVERAGE:")
                with
                | [] -> None
                | settings -> Some(List.head step, settings)
            )

        Expect.equal
            coverageByStep
            [
                "- name: Build via Bash", [ expected ]
                "- name: Build via Windows", [ expected ]
            ]
            "The Bash and Windows build steps should each collect coverage in the Debug leg only"

    let ``tests ran on each target framework`` testProjectName frameworks (d: DirectoryInfo) =
        let resultsDirectory =
            Path.Combine(d.FullName, "tests", testProjectName, "TestResults")

        let trxNamespace =
            XNamespace.Get "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"

        let requiredAttribute trx name (element: XElement) =
            match element.Attribute(XName.Get name) with
            | null -> failtestf "%s has a %s without a %s attribute" trx element.Name.LocalName name
            | attribute -> attribute.Value

        let passedFrameworks trx =
            let document = XDocument.Load(trx: string)

            let frameworkByTest =
                document.Descendants(
                    trxNamespace
                    + "UnitTest"
                )
                |> Seq.map (fun test ->
                    requiredAttribute trx "id" test,
                    Path.GetFileName(Path.GetDirectoryName(requiredAttribute trx "storage" test))
                )
                |> Map.ofSeq

            document.Descendants(
                trxNamespace
                + "UnitTestResult"
            )
            |> Seq.filter (fun result -> requiredAttribute trx "outcome" result = "Passed")
            |> Seq.map (fun result ->
                let testId = requiredAttribute trx "testId" result

                match Map.tryFind testId frameworkByTest with
                | Some framework -> framework
                | None -> failtestf "%s has a result for unknown test %s" trx testId
            )

        let testedFrameworks =
            Directory.GetFiles(resultsDirectory, "*.trx")
            |> Seq.collect passedFrameworks
            |> Set.ofSeq

        Expect.equal
            testedFrameworks
            (Set.ofList frameworks)
            $"{resultsDirectory} should hold passed test results for each library target framework"

    let ``assembly info values are set after pack`` projectName (d: DirectoryInfo) =
        let mutable extractedDllDir: string option = None

        let nupkgPath = Path.Combine(d.FullName, "dist", $"{projectName}.0.1.0.nupkg")

        Expect.isTrue (File.Exists nupkgPath) (sprintf "%s should exist" nupkgPath)

        use archive = ZipFile.OpenRead(nupkgPath)

        let entry =
            archive.Entries
            |> Seq.tryFind (fun x ->
                x.FullName.StartsWith("lib/net10.0/")
                && x.FullName.EndsWith($"/{projectName}.dll")
            )

        let dllPath =
            match entry with
            | Some e ->
                let tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))

                let tempPath = Path.Combine(tempDir, $"{projectName}.dll")

                Directory.CreateDirectory(tempDir)
                |> ignore

                e.ExtractToFile(tempPath, true)
                extractedDllDir <- Some tempDir

                tempPath
            | None -> failtestf "Could not find lib/net10.0/%s.dll in %s" projectName nupkgPath

        let assemblyName = AssemblyName.GetAssemblyName(dllPath)
        Expect.equal assemblyName.Version (Version "0.1.0.0") "AssemblyVersion should be 0.1.0.0"

        let fileVersionInfo = FileVersionInfo.GetVersionInfo(dllPath)

        Expect.isTrue
            (fileVersionInfo.FileVersion = "0.1.0"
             || fileVersionInfo.FileVersion = "0.1.0.0")
            "FileVersion should be 0.1.0 or 0.1.0.0"

        let assemblyContext =
            new AssemblyLoadContext($"metadata-{projectName}-{Guid.NewGuid():N}", true)

        try
            use assemblyStream = new MemoryStream(File.ReadAllBytes dllPath)
            let assembly = assemblyContext.LoadFromStream(assemblyStream)

            let title =
                assembly.GetCustomAttribute<AssemblyTitleAttribute>()
                |> Option.ofObj
                |> Option.map _.Title

            Expect.equal title (Some projectName) "AssemblyTitle should match project name"

            let product =
                assembly.GetCustomAttribute<AssemblyProductAttribute>()
                |> Option.ofObj
                |> Option.map _.Product

            Expect.equal product (Some projectName) "AssemblyProduct should match project name"

            let informationalVersion =
                assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                |> Option.ofObj
                |> Option.map _.InformationalVersion

            Expect.equal informationalVersion (Some "0.1.0") "InformationalVersion should be 0.1.0"

            let metadata =
                assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                |> Seq.map (fun x -> x.Key, x.Value)
                |> Map.ofSeq

            Expect.equal
                (metadata
                 |> Map.tryFind "ReleaseChannel")
                (Some "release")
                "ReleaseChannel should be release"

            let releaseDate =
                metadata
                |> Map.tryFind "ReleaseDate"

            Expect.isTrue
                (releaseDate
                 |> Option.exists (
                     String.IsNullOrWhiteSpace
                     >> not
                 ))
                "ReleaseDate should be set"
        finally
            assemblyContext.Unload()
            GC.Collect()
            GC.WaitForPendingFinalizers()
            GC.Collect()

            extractedDllDir
            |> Option.iter (fun path ->
                let rec deleteWithRetry attempts =
                    try
                        if Directory.Exists path then
                            Directory.Delete(path, true)
                    with
                    | :? IOException
                    | :? UnauthorizedAccessException when attempts > 0 ->
                        GC.Collect()
                        GC.WaitForPendingFinalizers()
                        System.Threading.Thread.Sleep deleteRetryDelayMs

                        deleteWithRetry (
                            attempts
                            - 1
                        )

                deleteWithRetry maxDeleteRetries
            )

module Effect =
    open System
    open Fake.IO
    open Fake.Tools
    open Fake.Core
    open Fake.DotNet
    open Infrastructure.Dotnet

    let ``dotnet run`` argStr projectPath (d: DirectoryInfo) =

        DotNet.exec
            (fun opt -> {
                opt with
                    WorkingDirectory = Path.Join(d.FullName, projectPath)
            })
            "run"
            argStr
        |> failOnBadExitAndPrint

    let ``dotnet new`` argsStr (path: string) (d: DirectoryInfo) =

        New.cmd
            (fun opt -> {
                opt with
                    WorkingDirectory = Path.Join(d.FullName, path)
            })
            argsStr

    let ``dotnet add reference`` args path (d: DirectoryInfo) =
        let args =
            Arguments.Empty
            |> Arguments.appendNotEmpty "reference" args

        DotNet.exec
            (fun opt -> {
                opt with
                    WorkingDirectory = Path.Join(d.FullName, path)
            })
            "add"
            args.ToStartInfo
        |> failOnBadExitAndPrint

    let ``dotnet sln add`` argsStr (d: DirectoryInfo) =
        let args =
            Arguments.Empty
            |> Arguments.appendNotEmpty "add" argsStr

        DotNet.exec
            (fun opt -> {
                opt with
                    WorkingDirectory = d.FullName
            })
            "sln"
            args.ToStartInfo
        |> failOnBadExitAndPrint

    let ``format build`` (d: DirectoryInfo) = Dotnet.fantomas d.FullName "build"


    let ``replace section with`` startIdx endIdx replace (data: string array) =
        printfn "replacing lines %i to %i with %s" startIdx endIdx replace

        for i = startIdx to endIdx do
            data.[i] <- sprintf "// %s" data.[i]

        Array.insert replace (endIdx + 1) data

    let ``find section`` startPredicate endPredicate data =
        let datai =
            data
            |> Array.indexed

        match
            datai
            |> Array.tryFind (startPredicate)
        with
        | None -> None
        | Some(startIdx, _) ->
            let (endIdx, _) =
                datai
                |> Array.skip (
                    startIdx
                    + 1
                )
                |> Array.takeWhile (endPredicate)
                |> Array.last

            Some(startIdx, endIdx)

    let ``get build.fs`` (d: DirectoryInfo) =
        d.FullName
        </> "build"
        </> "build.fs"


    let ``transform build script with`` (replaceFn: string -> string) (d: DirectoryInfo) =
        let buildScript = ``get build.fs`` d

        buildScript
        |> File.applyReplace replaceFn

    let ``disable restore`` (d: DirectoryInfo) =
        let buildScript = ``get build.fs`` d
        let lines = File.ReadAllLines buildScript
        let startPred = (fun (idx, line: string) -> line.Contains "let dotnetRestore")

        let endPred =
            (snd
             >> (fun (x: string) -> x.StartsWith "let ")
             >> not)

        match ``find section`` startPred endPred lines with
        | None -> ()
        | Some(startIdx, endIdx) ->
            ``replace section with``
                (startIdx
                 + 1)
                endIdx
                "    ()"
                lines
            |> File.writeNew buildScript

        ``format build`` d

    let ``disable build`` (d: DirectoryInfo) =
        let buildScript = ``get build.fs`` d
        let lines = File.ReadAllLines buildScript
        let startPred = (fun (idx, line: string) -> line.Contains "let dotnetBuild")

        let endPred =
            (snd
             >> (fun (x: string) -> x.StartsWith "let ")
             >> not)

        match ``find section`` startPred endPred lines with
        | None -> ()
        | Some(startIdx, endIdx) ->
            ``replace section with``
                (startIdx
                 + 1)
                endIdx
                "    ()"
                lines
            |> File.writeNew buildScript

        ``format build`` d

    let ``disable tests`` (d: DirectoryInfo) =
        let buildScript = ``get build.fs`` d
        let lines = File.ReadAllLines buildScript
        let startPred = (fun (idx, line: string) -> line.Contains "let dotnetTest")

        let endPred =
            (snd
             >> (fun (x: string) -> x.StartsWith "let ")
             >> not)

        match ``find section`` startPred endPred lines with
        | None -> ()
        | Some(startIdx, endIdx) ->
            ``replace section with``
                (startIdx
                 + 1)
                endIdx
                "    ()"
                lines
            |> File.writeNew buildScript

        ``format build`` d


    let ``disable generateCoverage`` (d: DirectoryInfo) =
        let buildScript = ``get build.fs`` d
        let lines = File.ReadAllLines buildScript

        let startPred =
            (fun (idx, line: string) -> line.Contains "let generateCoverageReport")

        let endPred =
            (snd
             >> (fun (x: string) -> x.StartsWith "let ")
             >> not)

        match ``find section`` startPred endPred lines with
        | None -> ()
        | Some(startIdx, endIdx) ->
            ``replace section with``
                (startIdx
                 + 1)
                endIdx
                "    ()"
                lines
            |> File.writeNew buildScript

        ``format build`` d


    let ``disable createPackages`` (d: DirectoryInfo) =
        let buildScript = ``get build.fs`` d
        let lines = File.ReadAllLines buildScript
        let startPred = (fun (idx, line: string) -> line.Contains "let createPackages")

        let endPred =
            (snd
             >> (fun (x: string) -> x.StartsWith "let ")
             >> not)

        match ``find section`` startPred endPred lines with
        | None -> ()
        | Some(startIdx, endIdx) ->
            ``replace section with``
                (startIdx
                 + 1)
                endIdx
                "    ()"
                lines
            |> File.writeNew buildScript

        ``format build`` d


    let ``disable dotnetPack`` (d: DirectoryInfo) =
        let buildScript = ``get build.fs`` d
        let lines = File.ReadAllLines buildScript
        let startPred = (fun (idx, line: string) -> line.Contains "let dotnetPack")

        let endPred =
            (snd
             >> (fun (x: string) -> x.StartsWith "let ")
             >> not)

        match ``find section`` startPred endPred lines with
        | None -> ()
        | Some(startIdx, endIdx) ->
            ``replace section with``
                (startIdx
                 + 1)
                endIdx
                "    ()"
                lines
            |> File.writeNew buildScript

        ``format build`` d

    let ``disable sourceLinkTest function`` (d: DirectoryInfo) =
        let buildScript = ``get build.fs`` d
        let lines = File.ReadAllLines buildScript

        match
            lines
            |> Array.tryFindIndex (fun line -> line.Contains "let sourceLinkTest")
        with
        | None -> ()
        | Some startIdx ->
            let mutable i =
                startIdx
                + 1

            let mutable keepGoing = true

            while keepGoing
                  && i < Array.length lines do
                if String.IsNullOrWhiteSpace lines.[i] then
                    lines.[i] <- "    ()"
                    keepGoing <- false
                else
                    lines.[i] <-
                        "// "
                        + lines.[i]

                i <- i + 1

            lines
            |> File.writeNew buildScript

        ``format build`` d

    let ``disable publishToNuget function`` (d: DirectoryInfo) =
        let buildScript = ``get build.fs`` d
        let lines = File.ReadAllLines buildScript

        match
            lines
            |> Array.tryFindIndex (fun line -> line.Contains "DotNet.nugetPush")
        with
        | None -> ()
        | Some startIdx ->
            let mutable i = startIdx
            let mutable keepGoing = true

            while keepGoing
                  && i < Array.length lines do
                if lines.[i].Trim() = "\"*.nupkg\"" then
                    keepGoing <- false
                // Then fall through to comment out this line, too
                lines.[i] <-
                    "// "
                    + lines.[i]

                i <- i + 1

            lines
            |> File.writeNew buildScript

        ``format build`` d

    let ``disable pushing in gitRelease function`` (d: DirectoryInfo) =
        let buildScript = ``get build.fs`` d
        let lines = File.ReadAllLines buildScript

        match
            lines
            |> Array.tryFindIndex (fun line -> line.Contains "let gitRelease")
        with
        | None -> ()
        | Some startIdx ->
            let mutable i =
                startIdx
                + 1

            let mutable keepGoing = true

            while keepGoing
                  && i < Array.length lines do
                if lines.[i].StartsWith("let githubRelease") then
                    keepGoing <- false
                else if lines.[i].Trim().StartsWith("Git.Branches.push") then
                    lines.[i] <-
                        "// "
                        + lines.[i]

                i <- i + 1

            lines
            |> File.writeNew buildScript

        ``format build`` d

    let ``change githubRelease function to only run release checks`` (d: DirectoryInfo) =
        let buildScript = ``get build.fs`` d
        let lines = File.ReadAllLines buildScript

        let githubReleaseStartIndexOpt =
            lines
            |> Array.tryFindIndex (fun line -> line.Contains "let githubRelease")

        let githubReleaseEndIndexOpt =
            githubReleaseStartIndexOpt
            |> Option.bind (fun startIndex ->
                lines
                |> Array.skip startIndex
                |> Array.tryFindIndex (fun line -> line.Contains "|> Async.RunSynchronously")
                |> Option.map (fun endIndex ->
                    endIndex
                    + startIndex
                )
            )

        match (githubReleaseStartIndexOpt, githubReleaseEndIndexOpt) with
        | Some startIdx, Some endIdx ->
            lines
            |> ``replace section with`` startIdx endIdx "let githubRelease _ = allReleaseChecks ()"
            |> File.writeNew buildScript

            ``format build`` d
        | _ -> failwith "couldn't find bounds of `let githubRelease` function in build.fs"

    let ``git init`` (d: DirectoryInfo) (branchName: string) =
        Git.CommandHelper.runGitCommand d.FullName $"init --initial-branch={branchName}"
        |> ignore

        Git.CommandHelper.runGitCommand d.FullName "config --local user.email nobody@example.org"
        |> ignore

        Git.CommandHelper.runGitCommand d.FullName "config --local user.name TestUser"
        |> ignore

    let ``git commit all`` message (d: DirectoryInfo) =
        Git.Staging.stageAll d.FullName
        Git.Commit.exec d.FullName message

    // The SDK maps PDB source paths to /_/ in a CI build only when the project has a source root, which git provides
    let ``commit the repository`` (d: DirectoryInfo) =
        let fixture = d.FullName

        // Git resolves a relative value against the fixture, as in a hook that sets GIT_INDEX_FILE=.git/index.lock
        for name in
            [
                "GIT_DIR"
                "GIT_WORK_TREE"
                "GIT_INDEX_FILE"
                "GIT_COMMON_DIR"
                "GIT_OBJECT_DIRECTORY"
            ] do
            match Environment.GetEnvironmentVariable name with
            | null -> ()
            | value ->
                let relative =
                    Path.GetRelativePath(fixture, Path.GetFullPath(Path.Combine(fixture, value)))

                Expect.isFalse
                    (relative = ".."
                     || relative.StartsWith(
                         ".."
                         + string Path.DirectorySeparatorChar
                     )
                     || Path.IsPathRooted relative)
                    $"{name}={value} resolves outside the fixture, so its git commands would write to another repository"

        ``git init`` d "main"
        ``git commit all`` "Initial commit" d

        let ok, prefix, error =
            Git.CommandHelper.runGitCommand d.FullName "rev-parse --show-prefix"

        Expect.isTrue ok $"git rev-parse failed in {d.FullName}: {error}"

        Expect.all
            prefix
            String.IsNullOrWhiteSpace
            $"The fixture should be the root of its own git repository, not {prefix}"

    let ``make build function fail`` (failureFunction: string) (d: DirectoryInfo) =
        let buildScript = ``get build.fs`` d
        let lines = File.ReadAllLines buildScript

        let idx =
            lines
            |> Array.findIndex (fun line -> line.Contains failureFunction)

        let msg =
            sprintf "    failwith \"Deliberate failure in unit test for %s\"\n" failureFunction

        Array.insert msg (idx + 1) lines
        // |> fun lines -> lines |> Seq.iter(printfn "%s") ; lines
        |> File.writeNew buildScript

        ``format build`` d

    let ``make failure in gitRelease function`` (failureCode: string) (d: DirectoryInfo) =
        let buildScript = ``get build.fs`` d
        let lines = File.ReadAllLines buildScript

        match
            lines
            |> Array.tryFindIndex (fun line -> line.Contains "let gitRelease")
        with
        | None -> ()
        | Some startIdx ->
            let mutable i =
                startIdx
                + 1

            let mutable keepGoing = true

            while keepGoing
                  && i < Array.length lines do
                if lines.[i].StartsWith("let githubRelease") then
                    keepGoing <- false
                elif lines.[i].Contains(failureCode) then
                    let fail = "    failwith \"Deliberate failure in unit test for gitRelease\"\n"

                    lines.[i] <-
                        fail
                        + "// "
                        + lines.[i]

                i <- i + 1

            lines
            |> File.writeNew buildScript

        ``format build`` d

    let ``set environment variable`` name value (d: DirectoryInfo) =
        Environment.SetEnvironmentVariable(name, value)

    let ``add change to CHANGELOG`` (d: DirectoryInfo) =
        let changelog = Path.combine d.FullName "CHANGELOG.md"
        let lines = File.ReadAllLines changelog

        match
            lines
            |> Array.tryFindIndex (fun line -> line.Contains "## [Unreleased]")
        with
        | None -> ()
        | Some startIdx ->
            let newLines =
                lines
                |> Array.insert
                    "### Changed"
                    (startIdx
                     + 1)
                |> Array.insert
                    "- This is a test change from (@TheAngryByrd)"
                    (startIdx
                     + 2)

            newLines
            |> File.writeNew changelog

    let private ``internal setup for all tests`` (branchName: string) (d: DirectoryInfo) =
        ``git init`` d branchName
        ``disable restore`` d
        ``disable build`` d
        ``disable tests`` d
        ``disable generateCoverage`` d
        ``disable createPackages`` d
        ``disable dotnetPack`` d
        ``git commit all`` "Initial commit" d
        // SourceLinkTest requires an actual repo to exist, which we won't have in these tests
        ``disable sourceLinkTest function`` d
        // We don't want to actually release anything during integration tests!
        ``set environment variable`` "NUGET_KEY" "" d
        ``set environment variable`` "GITHUB_TOKEN" "" d
        ``disable pushing in gitRelease function`` d

    let private ``internal setup for release tests`` (branchName: string) (d: DirectoryInfo) =
        ``internal setup for all tests`` branchName d
        ``set environment variable`` "RELEASE_VERSION" "2.0.0" d
        ``add change to CHANGELOG`` d

    let ``setup for release tests`` (d: DirectoryInfo) =
        ``internal setup for release tests`` "main" d

    let ``setup for release branch tests`` (branchName: string) (d: DirectoryInfo) =
        ``internal setup for release tests`` branchName d

    let ``setup for publish tests`` (d: DirectoryInfo) =
        ``internal setup for all tests`` "main" d
        ``disable publishToNuget function`` d
        // To make publish work locally during integration tests
        ``set environment variable`` "CI" "1" d

        for launcher in
            [
                "build.cmd"
                "build.sh"
            ] do
            let path = Path.Combine(d.FullName, launcher)
            let source = System.IO.File.ReadAllText path

            Expect.stringContains
                source
                "dotnet run --project"
                $"Expected one build invocation in {launcher}."

            let withTag =
                source.Replace(
                    "dotnet run --project",
                    "dotnet run --environment RELEASE_TAG=v0.1.0 --project"
                )

            System.IO.File.WriteAllText(path, withTag)
