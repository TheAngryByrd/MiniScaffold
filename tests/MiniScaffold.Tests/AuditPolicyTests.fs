namespace MiniScaffold.Tests

module AuditPolicyTests =
    open System
    open System.Diagnostics
    open System.IO
    open System.Net
    open System.Net.Sockets
    open System.Text
    open System.Text.Json
    open System.Text.RegularExpressions
    open System.Threading
    open System.Xml.Linq
    open Expecto
    open Infrastructure

    type private Host =
        | Root
        | Library
        | Console

    type private ProjectRole =
        | Source
        | Build
        | Test

    type private AuditScenario = {
        Host: Host
        Role: ProjectRole
        CI: bool
    }

    type private PackageIdentity = { Id: string; Version: string }

    type private CommandResult = { ExitCode: int; Output: string }

    type private GeneratedProject = {
        Directory: string
        ProjectFile: string
    }

    let private repository =
        Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "../.."))

    let private templateSetup =
        lazy
            (Tests.tests
             |> ignore)

    let private runDotnet workingDirectory ci arguments =
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

        startInfo.Environment.["CI"] <- if ci then "true" else "false"
        startInfo.Environment.["WarningsAsErrors"] <- "NU1605"
        startInfo.Environment.["WarningsNotAsErrors"] <- "NU1801"
        startInfo.Environment.["NoWarn"] <- "NU1701"
        startInfo.Environment.["DOTNET_CLI_UI_LANGUAGE"] <- "en-US"

        startInfo.Environment.Remove "MSBuildSDKsPath"
        |> ignore

        startInfo.Environment.Remove "MSBuildExtensionsPath"
        |> ignore

        use child = Process.Start startInfo
        let output = child.StandardOutput.ReadToEndAsync()
        let errors = child.StandardError.ReadToEndAsync()
        child.WaitForExit()

        {
            ExitCode = child.ExitCode
            Output =
                output.GetAwaiter().GetResult()
                + errors.GetAwaiter().GetResult()
        }

    type private AdvisoryFeed(packageId: string, version: string, severities: int list) =
        let listener = new TcpListener(IPAddress.Loopback, 0)
        let cancellation = new CancellationTokenSource()
        do listener.Start()
        let port = (listener.LocalEndpoint :?> IPEndPoint).Port
        let address = $"http://127.0.0.1:{port}"

        let serviceIndex =
            JsonSerializer.Serialize(
                dict [
                    "version", box "3.0.0"
                    "resources",
                    box [|
                        dict [
                            "@id",
                            address
                            + "/vulnerabilities.json"
                            "@type", "VulnerabilityInfo/6.7.0"
                        ]
                    |]
                ]
            )

        let vulnerabilityIndex =
            JsonSerializer.Serialize [|
                dict [
                    "@name", "audit-policy"
                    "@id",
                    address
                    + "/data.json"
                    "@updated", "2026-01-01T00:00:00Z"
                ]
            |]

        let data =
            severities
            |> List.map (fun severity -> {|
                url = $"https://example.invalid/audit-policy/{severity}"
                severity = severity
                versions = $"[{version}]"
            |})
            |> fun advisories -> dict [ packageId.ToLowerInvariant(), advisories ]
            |> JsonSerializer.Serialize

        let serving =
            task {
                try
                    while not cancellation.IsCancellationRequested do
                        use! client = listener.AcceptTcpClientAsync(cancellation.Token)
                        use stream = client.GetStream()
                        use reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true)
                        let! request = reader.ReadLineAsync(cancellation.Token)
                        let mutable header = "request"

                        while not (String.IsNullOrEmpty header) do
                            let! line = reader.ReadLineAsync(cancellation.Token)
                            header <- line

                        let body =
                            match request.Split(' ').[1] with
                            | "/index.json" -> serviceIndex
                            | "/vulnerabilities.json" -> vulnerabilityIndex
                            | "/data.json" -> data
                            | path -> failwithf "Unexpected audit feed request: %s" path

                        let bytes = Encoding.UTF8.GetBytes body

                        let headers =
                            Encoding.ASCII.GetBytes(
                                $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n"
                            )

                        do! stream.WriteAsync(headers, cancellation.Token)
                        do! stream.WriteAsync(bytes, cancellation.Token)
                with
                | :? OperationCanceledException when cancellation.IsCancellationRequested -> ()
                | :? SocketException when cancellation.IsCancellationRequested -> ()
            }

        member _.Index =
            address
            + "/index.json"

        interface IDisposable with
            member _.Dispose() =
                cancellation.Cancel()
                listener.Stop()
                serving.GetAwaiter().GetResult()
                cancellation.Dispose()

    let private setAuditSource directory source =
        let path = Path.Combine(directory, "NuGet.config")
        let config = XDocument.Load path

        config.Root.Elements(XName.Get "auditSources")
        |> Seq.toList
        |> List.iter (fun element -> element.Remove())

        config.Root.Add(
            XElement(
                XName.Get "auditSources",
                XElement(XName.Get "clear"),
                XElement(
                    XName.Get "add",
                    XAttribute(XName.Get "key", "audit-policy"),
                    XAttribute(XName.Get "value", source),
                    XAttribute(XName.Get "allowInsecureConnections", "true")
                )
            )
        )

        config.Save path

    let private generateProject (directory: Disposables.DisposableDirectory) scenario =
        Tests.copyGlobalJson directory.DirectoryInfo

        match scenario.Host with
        | Root ->
            for name in
                [
                    "Directory.Build.props"
                    "Directory.Packages.props"
                    "NuGet.config"
                ] do
                File.Copy(Path.Combine(repository, name), Path.Combine(directory.Directory, name))

            let project = Path.Combine(directory.Directory, "build.fsproj")
            File.Copy(Path.Combine(repository, "build/build.fsproj"), project)

            {
                Directory = directory.Directory
                ProjectFile = project
            }
        | Library
        | Console ->
            templateSetup.Force()
            let outputType = if scenario.Host = Library then "library" else "console"

            Tests.runTemplate
                directory.Directory
                $"-n AuditFixture --githubUsername TestUser --outputType {outputType}"

            let solution = Path.Combine(directory.Directory, "AuditFixture")

            let relativePath =
                match scenario.Role with
                | Source -> "src/AuditFixture/AuditFixture.fsproj"
                | Build -> "build/build.fsproj"
                | Test -> "tests/AuditFixture.Tests/AuditFixture.Tests.fsproj"

            {
                Directory = solution
                ProjectFile = Path.Combine(solution, relativePath)
            }

    let private properties directory project ci framework =
        let names =
            "TargetFramework,TargetFrameworks,NuGetAudit,NuGetAuditMode,NuGetAuditLevel,WarningsAsErrors,WarningsNotAsErrors,NoWarn"

        let arguments =
            [
                "msbuild"
                project
                "-nologo"
                $"-getProperty:{names}"
            ]
            @ (framework
               |> Option.map (fun value -> [ $"-p:TargetFramework={value}" ])
               |> Option.defaultValue [])

        let { ExitCode = code; Output = output } = runDotnet directory ci arguments
        Expect.equal code 0 output
        use document = JsonDocument.Parse output

        document.RootElement.GetProperty("Properties").EnumerateObject()
        |> Seq.map (fun property -> property.Name, property.Value.GetString())
        |> Map.ofSeq

    let private transitivePackage (project: string) (framework: string) =
        let path = Path.Combine(Path.GetDirectoryName project, "obj/project.assets.json")
        use assets = JsonDocument.Parse(File.ReadAllText path)

        let settings =
            assets.RootElement.GetProperty("project").GetProperty("frameworks").GetProperty
                framework

        let direct =
            settings.GetProperty("dependencies").EnumerateObject()
            |> Seq.map (fun property -> property.Name.ToLowerInvariant())
            |> Set.ofSeq

        let centrallyManaged =
            match settings.TryGetProperty "centralPackageVersions" with
            | true, packages ->
                packages.EnumerateObject()
                |> Seq.map (fun property -> property.Name.ToLowerInvariant())
                |> Set.ofSeq
            | _ -> Set.empty

        assets.RootElement.GetProperty("targets").GetProperty(framework).EnumerateObject()
        |> Seq.tryPick (fun package ->
            match package.Value.GetProperty("type").GetString(), package.Name.Split('/') with
            | "package", [| id; version |] when
                not (Set.contains (id.ToLowerInvariant()) direct)
                && not (Set.contains (id.ToLowerInvariant()) centrallyManaged)
                ->
                Some { Id = id; Version = version }
            | _ -> None
        )
        |> Option.defaultWith (fun () ->
            failtestf "No transitive audit fixture package in %s (%s)" project framework
        )

    let private verifyScenario scenario _ =
        use directory = Disposables.DisposableDirectory.Create()

        let {
                Directory = solution
                ProjectFile = project
            } =
            generateProject directory scenario

        let evaluated = properties solution project scenario.CI None

        let frameworks =
            if String.IsNullOrWhiteSpace evaluated.["TargetFrameworks"] then
                [| evaluated.["TargetFramework"] |]
            else
                evaluated.["TargetFrameworks"].Split(';')

        for framework in frameworks do
            let restoreArguments = [
                "restore"
                project
                "--force"
                "--no-http-cache"
                "--no-dependencies"
                "--verbosity"
                "minimal"
                "-p:RestoreLockedMode=false"
                $"-p:TargetFramework={framework}"
            ]

            let { ExitCode = code; Output = output } =
                runDotnet
                    solution
                    scenario.CI
                    (restoreArguments
                     @ [ "-p:NuGetAudit=false" ])

            Expect.equal code 0 output
            let package = transitivePackage project framework

            for severities in
                [
                    [
                        0
                        1
                    ]
                    [
                        0
                        1
                        2
                        3
                    ]
                ] do
                use feed = new AdvisoryFeed(package.Id, package.Version, severities)
                setAuditSource solution feed.Index

                let { ExitCode = code; Output = output } =
                    runDotnet solution scenario.CI restoreArguments

                let highIsError =
                    scenario.CI
                    || scenario.Role = Source

                let expectFailure =
                    highIsError
                    && List.contains 2 severities

                Expect.equal
                    code
                    (if expectFailure then 1 else 0)
                    $"{scenario}, {framework}, {package.Id}/{package.Version}\n{output}"

                for severity in severities do
                    let level =
                        if
                            severity
                            >= 2
                            && highIsError
                        then
                            "error"
                        else
                            "warning"

                    let warningCode =
                        $"NU190{severity
                                + 1}"

                    let pattern =
                        $"{level} {warningCode}:[^\r\n]*https://example\\.invalid/audit-policy/{severity}"

                    Expect.isTrue
                        (Regex.IsMatch(output, pattern))
                        $"Expected {level} {warningCode} for transitive {package.Id} ({framework}).\n{output}"

                    let opposite = if level = "error" then "warning" else "error"
                    Expect.isFalse (Regex.IsMatch(output, $"{opposite} {warningCode}:")) output

            let values = properties solution project scenario.CI (Some framework)
            Expect.equal values.["NuGetAudit"] "true" "Audit must remain enabled."

            Expect.equal
                values.["NuGetAuditMode"]
                "all"
                "All projects must audit transitive packages."

            Expect.equal values.["NuGetAuditLevel"] "low" "Low advisories must remain visible."

            for name, required in
                [
                    "WarningsAsErrors", [ "NU1605" ]
                    "WarningsNotAsErrors",
                    [
                        "NU1801"
                        "NU1901"
                        "NU1902"
                    ]
                    "NoWarn", [ "NU1701" ]
                ] do
                let warnings =
                    values.[name].Split(';')
                    |> Set.ofArray

                for warning in required do
                    Expect.contains warnings warning $"{name} must preserve {warning}."

    [<Tests>]
    let tests =
        testSequenced
        <| testList "Audit policy" [
            for host, roles in
                [
                    Root, [ Build ]
                    Library,
                    [
                        Source
                        Build
                        Test
                    ]
                    Console,
                    [
                        Source
                        Build
                        Test
                    ]
                ] do
                for role in roles do
                    for ci in
                        [
                            false
                            true
                        ] do
                        let scenario = { Host = host; Role = role; CI = ci }

                        testCase
                            $"{host} {role} CI={ci} transitive advisories"
                            (verifyScenario scenario)
        ]
