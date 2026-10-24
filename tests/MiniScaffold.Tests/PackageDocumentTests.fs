namespace MiniScaffold.Tests

module PackageDocumentTests =
    open System
    open System.Diagnostics
    open System.IO
    open System.IO.Compression
    open System.Xml.Linq
    open Expecto
    open Infrastructure

    type private PackageHost =
        | Console
        | Library

    let private runDotnet workingDirectory arguments =
        let startInfo =
            ProcessStartInfo(
                "dotnet",
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            )

        for argument in arguments do
            startInfo.ArgumentList.Add argument

        startInfo.Environment.Remove "MSBuildSDKsPath"
        |> ignore

        startInfo.Environment.Remove "MSBuildExtensionsPath"
        |> ignore

        startInfo.Environment["MSBUILDDISABLENODEREUSE"] <- "1"

        let result = ProcessCapture.run (TimeSpan.FromSeconds 30.) startInfo

        Expect.equal
            result.ExitCode
            0
            (sprintf
                "dotnet %s should succeed in %s.\nstdout:\n%s\nstderr:\n%s"
                (String.concat " " arguments)
                workingDirectory
                result.StandardOutput
                result.StandardError)

    let private packagesContainHostDocuments host _ =
        let outputType =
            match host with
            | Console -> "console"
            | Library -> "library"

        let solutionName = $"Document{host}"
        let addedLibraryName = "DocumentProjectLibrary"
        use generated = Disposables.DisposableDirectory.Create()
        Tests.copyGlobalJson generated.DirectoryInfo

        Tests.runTemplate
            generated.Directory
            $"-n {solutionName} --githubUsername TestUser --outputType {outputType}"

        let hostDirectory = Path.Combine(generated.Directory, solutionName)

        Tests.runTemplate
            (Path.Combine(hostDirectory, "src"))
            $"-n {addedLibraryName} --githubUsername TestUser --outputType projLib"

        let packageNames =
            match host with
            | Console -> [ addedLibraryName ]
            | Library -> [
                addedLibraryName
                solutionName
              ]

        for packageName in packageNames do
            let projectPath =
                Path.Combine(hostDirectory, "src", packageName, $"{packageName}.fsproj")

            let outputDirectory = Path.Combine(hostDirectory, "packed", packageName)

            runDotnet hostDirectory [
                "pack"
                projectPath
                "--output"
                outputDirectory
            ]

            let packages = Directory.GetFiles(outputDirectory, "*.nupkg")
            Expect.equal packages.Length 1 $"{packageName} should produce exactly one package"
            let packagePath = Array.exactlyOne packages
            use archive = ZipFile.OpenRead packagePath

            for documentName in
                [
                    "README.md"
                    "LICENSE.md"
                ] do
                let documents =
                    archive.Entries
                    |> Seq.filter (fun entry -> entry.Name = documentName)
                    |> Seq.toList

                Expect.equal
                    documents.Length
                    1
                    $"{packageName} should contain exactly one {documentName}"

                let document = List.exactlyOne documents

                Expect.equal
                    document.FullName
                    documentName
                    $"{packageName} should contain {documentName} at the package root"

                use documentStream = document.Open()
                use contents = new MemoryStream()
                documentStream.CopyTo contents

                Expect.sequenceEqual
                    (contents.ToArray())
                    (File.ReadAllBytes(Path.Combine(hostDirectory, documentName)))
                    $"{packageName} should preserve the host {documentName} bytes"

            let nuspecEntries =
                archive.Entries
                |> Seq.filter (fun entry ->
                    entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal)
                )
                |> Seq.toList

            Expect.equal nuspecEntries.Length 1 $"{packageName} should contain exactly one nuspec"
            use nuspecStream = (List.exactlyOne nuspecEntries).Open()
            let nuspec = XDocument.Load nuspecStream
            let ns = nuspec.Root.Name.Namespace

            let metadataElement name =
                let elements =
                    nuspec
                        .Descendants(
                            ns
                            + "metadata"
                        )
                        .Elements(ns + name)
                    |> Seq.toList

                Expect.equal elements.Length 1 $"{packageName} should declare {name} exactly once"
                List.exactlyOne elements

            let license = metadataElement "license"

            Expect.equal
                (license.Attribute(XName.Get "type")
                 |> Option.ofObj
                 |> Option.map _.Value)
                (Some "file")
                $"{packageName} should declare a file license"

            Expect.equal license.Value "LICENSE.md" $"{packageName} should name the root license"

            Expect.equal
                (metadataElement "readme").Value
                "README.md"
                $"{packageName} should name the root readme"

            for framework in
                [
                    "net8.0"
                    "net9.0"
                    "net10.0"
                ] do
                let assemblyPath = $"lib/{framework}/{packageName}.dll"

                Expect.equal
                    (archive.Entries
                     |> Seq.filter (fun entry -> entry.FullName = assemblyPath)
                     |> Seq.length)
                    1
                    $"{packageName} should contain exactly one {framework} assembly"

    [<Tests>]
    let tests =
        testSequenced
        <| testList "package documents" [
            testCase
                "Console-hosted projLib packs the host documents with default metadata"
                (packagesContainHostDocuments Console)
            testCase
                "Library-hosted libraries pack the host documents with default metadata"
                (packagesContainHostDocuments Library)
        ]
