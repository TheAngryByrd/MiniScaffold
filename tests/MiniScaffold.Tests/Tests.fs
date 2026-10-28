namespace MiniScaffold.Tests


module Tests =
    open System
    open System.Diagnostics
    open System.IO.Compression
    open System.Reflection.Metadata
    open System.Reflection.PortableExecutable
    open System.Runtime.InteropServices
    open System.Text.Json
    open System.Xml.Linq
    open Fake.Core
    open Expecto
    open Infrastructure

    let ptestCase (reason: string) name test = Expecto.Tests.ptestCase name test

    let logger = Expecto.Logging.Log.create "setup"
    let templateName = "mini-scaffold"

    let nugetPkgPath =
        match Environment.environVarOrNone "MINISCAFFOLD_NUPKG_LOCATION" with
        | Some v ->
            printfn "using MINISCAFFOLD_NUPKG_LOCATION"
            v
        | None ->
            let dist =
                IO.Path.Combine(__SOURCE_DIRECTORY__, "../../dist")
                |> IO.DirectoryInfo

            dist.EnumerateFiles("*.nupkg")
            |> Seq.head
            |> fun fi -> fi.FullName

    let debug () =
        if not (System.Diagnostics.Debugger.IsAttached) then
            printfn
                "Please attach a debugger, PID: %d"
                (System.Diagnostics.Process.GetCurrentProcess().Id)

        while not (System.Diagnostics.Debugger.IsAttached) do
            System.Threading.Thread.Sleep(100)

        System.Diagnostics.Debugger.Break()
        ()

    let showTemplateHelp directory =
        let newArgs =
            Arguments.Empty
            |> Arguments.append [
                templateName
                "--help"
            ]

        let opts =
            match directory with
            | Some d -> (fun (opt: Fake.DotNet.DotNet.Options) -> { opt with WorkingDirectory = d })
            | None -> id

        Fake.DotNet.DotNet.getVersion (fun vOpt -> vOpt.WithCommon opts)
        |> printfn "dotnet --version %s"

        Dotnet.New.cmd opts newArgs.ToStartInfo

    let setup () =
        // debug ()
        // ensure we're installing the one from our dist folder
        printfn "nugetPkgPath %s" nugetPkgPath

        Fake.DotNet.DotNet.getSDKVersionFromGlobalJson ()
        |> printfn "dotnet global.json version %s"

        Fake.DotNet.DotNet.getVersion id
        |> printfn "dotnet --version %s"

        printfn "Installing template..."
        Dotnet.New.install nugetPkgPath
        printfn "Installed template..."

        showTemplateHelp None

    let runTemplate (directory) args =
        let newArgs =
            Arguments.Empty
            |> Arguments.append [ templateName ]
            // |> Arguments.appendNotEmpty "-lang" "F#"
            |> Arguments.appendRaw args

        Dotnet.New.cmd
            (fun opt -> {
                opt with
                    WorkingDirectory = directory
            })
            newArgs.ToStartInfo

    let copyGlobalJson (directory: IO.DirectoryInfo) =
        let globalJson = IO.Path.Join(__SOURCE_DIRECTORY__, "../../global.json")
        let destination = IO.Path.Join(directory.FullName, "global.json")
        IO.File.Copy(globalJson, destination)

    let projectStructureAsserts = [
        Assert.``CHANGELOG exists``
        Assert.``.config/dotnet-tools.json exists``
        Assert.``.github ISSUE_TEMPLATE bug_report exists``
        Assert.``.github ISSUE_TEMPLATE feature_request exists``
        Assert.``.github workflows build exists``
        Assert.``.github ISSUE_TEMPLATE exists``
        Assert.``.github PULL_REQUEST_TEMPLATE exists``
        Assert.``.editorconfig exists``
        Assert.``.gitattributes exists``
        Assert.``.gitignore exists``
        Assert.``LICENSE exists``
        Assert.``NuGet.config exists``
        Assert.``Directory.Packages.props exists``
        Assert.``README exists``
    ]

    type private PackageHost =
        | Library
        | Console

    let private packedLibrariesSupplyFSharpCore host _ =
        let outputType, solutionName, packageNames =
            match host with
            | Library ->
                "library",
                "DependencyLibrary",
                [
                    "DependencyLibrary"
                    "DependencyProjectLibrary"
                ]
            | Console -> "console", "DependencyConsole", [ "DependencyProjectLibrary" ]

        use generated = Disposables.DisposableDirectory.Create()
        copyGlobalJson generated.DirectoryInfo

        runTemplate
            generated.Directory
            $"-n {solutionName} --githubUsername TestUser --outputType {outputType}"

        let projectDir =
            IO.DirectoryInfo(IO.Path.Combine(generated.Directory, solutionName))

        Effect.``dotnet new``
            "mini-scaffold -n DependencyProjectLibrary --githubUsername TestUser --outputType projLib"
            "src"
            projectDir

        Effect.``dotnet sln add``
            "src/DependencyProjectLibrary/DependencyProjectLibrary.fsproj"
            projectDir

        let runDotnet context workingDirectory environment arguments =
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

            for key, value in environment do
                startInfo.Environment.[key] <- value

            startInfo.Environment.Remove "MSBuildSDKsPath"
            |> ignore

            startInfo.Environment.Remove "MSBuildExtensionsPath"
            |> ignore

            startInfo.Environment["MSBUILDDISABLENODEREUSE"] <- "1"

            let result =
                try
                    ProcessCapture.run (TimeSpan.FromSeconds 30.) startInfo
                with ex ->
                    raise (InvalidOperationException($"{context}: {ex.Message}", ex))

            Expect.equal
                result.ExitCode
                0
                (sprintf
                    "%s dotnet %s failed in %s.\nstdout:\n%s\nstderr:\n%s"
                    context
                    (String.concat " " arguments)
                    workingDirectory
                    result.StandardOutput
                    result.StandardError)

        match host with
        | Library -> Builds.executeBuild projectDir.FullName "DotnetPack"
        | Console ->
            Builds.executeBuild projectDir.FullName "DotnetTest"

            runDotnet "Console-hosted library pack" projectDir.FullName [] [
                "pack"
                "src/DependencyProjectLibrary/DependencyProjectLibrary.fsproj"
                "--configuration"
                "Release"
                "--output"
                IO.Path.Combine(projectDir.FullName, "dist")
                "-p:PackageVersion=0.1.0"
                "-p:PackageLicenseFile="
                "-p:PackageReadmeFile="
            ]


        let attribute name (element: XElement) =
            match element.Attribute(XName.Get name) with
            | null -> failtestf "%s is missing attribute %s" (string element) name
            | value -> value.Value

        let centralPackages =
            XDocument.Load(IO.Path.Combine(projectDir.FullName, "Directory.Packages.props"))

        let coreVersion =
            centralPackages.Descendants(XName.Get "PackageVersion")
            |> Seq.filter (fun package -> attribute "Include" package = "FSharp.Core")
            |> Seq.exactlyOne
            |> attribute "Version"

        let consumerSource =
            """using System;
using Say = PackageUnderTest.Say;

var person = new Say.Person("Ada", 42, Say.FavoriteColor.Blue,
    new DateTimeOffset(2000, 1, 2, 0, 0, 0, TimeSpan.Zero));
var actual = Say.helloPerson(person);
const string expected = "Hello Ada. You were born on 2000/01/02 and your favorite number is 42. You like Blue.";
if (actual != expected)
    throw new Exception($"Expected '{expected}', but got '{actual}'.");
"""

        for projectName in packageNames do
            let project =
                XDocument.Load(
                    IO.Path.Combine(
                        projectDir.FullName,
                        "src",
                        projectName,
                        $"{projectName}.fsproj"
                    )
                )

            let frameworks =
                project.Descendants(XName.Get "TargetFrameworks")
                |> Seq.exactlyOne
                |> fun value ->
                    value.Value.Split(';')
                    |> Array.toList

            let packagePath =
                IO.Path.Combine(projectDir.FullName, "dist", $"{projectName}.0.1.0.nupkg")

            Expect.isTrue (IO.File.Exists packagePath) $"{packagePath} should exist"
            use archive = ZipFile.OpenRead packagePath
            use nuspecStream = archive.GetEntry($"{projectName}.nuspec").Open()
            let nuspec = XDocument.Load nuspecStream
            let ns = nuspec.Root.Name.Namespace

            let groups =
                nuspec.Descendants(
                    ns
                    + "group"
                )
                |> Seq.toList

            Expect.equal
                (groups
                 |> List.map (attribute "targetFramework")
                 |> List.sort)
                (frameworks
                 |> List.sort)
                $"{packagePath} should contain exactly one dependency group for each target framework"

            for group in groups do
                let framework = attribute "targetFramework" group

                let dependencies =
                    group.Elements(
                        ns
                        + "dependency"
                    )
                    |> Seq.filter (fun dependency -> attribute "id" dependency = "FSharp.Core")
                    |> Seq.toList

                let context = $"{projectName} {framework}"

                Expect.equal
                    dependencies.Length
                    1
                    $"{context} should declare FSharp.Core exactly once"

                let dependency = List.exactlyOne dependencies

                Expect.equal
                    (attribute "version" dependency)
                    coreVersion
                    $"{context} should use the central FSharp.Core version as its minimum dependency version"

                let assets name defaults =
                    match dependency.Attribute(XName.Get name) with
                    | null -> defaults
                    | value ->
                        value.Value.Split(
                            [|
                                ','
                                ';'
                            |],
                            StringSplitOptions.RemoveEmptyEntries
                        )
                        |> Seq.map (fun asset -> asset.Trim().ToLowerInvariant())
                        |> Set.ofSeq

                let included = assets "include" (Set.singleton "all")
                let excluded = assets "exclude" Set.empty

                for asset in
                    [
                        "compile"
                        "runtime"
                    ] do
                    Expect.isTrue
                        ((Set.contains "all" included
                          || Set.contains asset included)
                         && not (
                             Set.contains "all" excluded
                             || Set.contains asset excluded
                         ))
                        $"{context} should supply FSharp.Core {asset} assets"

            use consumer = Disposables.DisposableDirectory.Create()
            copyGlobalJson consumer.DirectoryInfo
            let element name (content: obj array) = XElement(XName.Get name, content)

            let consumerProject =
                element "Project" [|
                    XAttribute(XName.Get "Sdk", "Microsoft.NET.Sdk")
                    element "PropertyGroup" [|
                        element "OutputType" [| "Exe" |]
                        element "TargetFramework" [| "net8.0" |]
                    |]
                    element "ItemGroup" [|
                        element "PackageReference" [|
                            XAttribute(XName.Get "Include", projectName)
                            XAttribute(XName.Get "Version", "0.1.0")
                        |]
                    |]
                |]

            consumerProject.Save(IO.Path.Combine(consumer.Directory, "Consumer.csproj"))

            IO.File.WriteAllText(
                IO.Path.Combine(consumer.Directory, "Program.cs"),
                consumerSource.Replace("PackageUnderTest", projectName)
            )

            let config =
                element "configuration" [|
                    element "packageSources" [|
                        element "clear" [||]
                        element "add" [|
                            XAttribute(XName.Get "key", "generated")
                            XAttribute(
                                XName.Get "value",
                                IO.Path.Combine(projectDir.FullName, "dist")
                            )
                        |]
                        element "add" [|
                            XAttribute(XName.Get "key", "nuget.org")
                            XAttribute(XName.Get "value", "https://api.nuget.org/v3/index.json")
                        |]
                    |]
                    element "fallbackPackageFolders" [| element "clear" [||] |]
                |]

            let configPath = IO.Path.Combine(consumer.Directory, "NuGet.config")
            config.Save configPath

            let runConsumer =
                runDotnet $"{projectName} consumer" consumer.Directory [
                    "NUGET_PACKAGES", IO.Path.Combine(consumer.Directory, "packages")
                ]

            runConsumer [
                "restore"
                "--configfile"
                configPath
                "--no-http-cache"
            ]

            runConsumer [
                "build"
                "--no-restore"
            ]

            runConsumer [
                "run"
                "--no-build"
                "--no-restore"
            ]

    let private consoleCommandsPreserveOptionalMetadata _ =
        use generated = Disposables.DisposableDirectory.Create()
        copyGlobalJson generated.DirectoryInfo
        let projectName = "MetadataConsole"

        runTemplate
            generated.Directory
            $"-n {projectName} --githubUsername TestUser --outputType console"

        let projectDirectory = IO.Path.Combine(generated.Directory, projectName)

        let projectPath =
            IO.Path.Combine(projectDirectory, "src", projectName, $"{projectName}.fsproj")

        let outputDirectory = IO.Path.Combine(projectDirectory, "runtime-output")
        let assemblyPath = IO.Path.Combine(outputDirectory, $"{projectName}.dll")

        let runDotnet arguments =
            let startInfo =
                ProcessStartInfo(
                    "dotnet",
                    WorkingDirectory = projectDirectory,
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

            {|
                Arguments = String.concat " " arguments
                ExitCode = result.ExitCode
                Stdout = result.StandardOutput
                Stderr = result.StandardError
            |}

        let expectSuccess
            (result:
                {|
                    Arguments: string
                    ExitCode: int
                    Stdout: string
                    Stderr: string
                |})
            =
            Expect.equal
                result.ExitCode
                0
                $"dotnet {result.Arguments} should succeed.\n{result.Stdout}\n{result.Stderr}"

        let runCommand arguments =
            let result =
                runDotnet (
                    assemblyPath
                    :: arguments
                )

            expectSuccess result
            Expect.isEmpty result.Stderr $"The command should not write to stderr.\n{result.Stderr}"
            result.Stdout.TrimEnd('\r', '\n')

        let check (attributes: (string * string) list) expectedVersion expectedMetadata =
            runDotnet [
                "build"
                projectPath
                "--configuration"
                "Release"
                "--output"
                outputDirectory
                if not (List.isEmpty attributes) then
                    $"-p:AssemblyVersion={expectedVersion}"
                for key, value in attributes do
                    $"-p:AssemblyMetadata{key}={value}"
            ]
            |> expectSuccess

            Expect.equal
                (runCommand [ "--info" ])
                $"{projectName} - {expectedVersion} - {expectedMetadata}"
                "--info should preserve supplied metadata and display defaults for absent attributes"

            Expect.equal
                (runCommand [ "--version" ])
                expectedVersion
                "--version should preserve the assembly version"

            Expect.equal
                (runCommand [
                    "--favorite-color"
                    "White"
                    "World"
                ])
                "Hello World"
                "The main command should preserve its greeting"

            Expect.equal
                (runCommand [ "World" ])
                "Hello World"
                "The greeting should use the default favorite color from App.config"

            let help = runCommand [ "--help" ]
            Expect.stringContains help "USAGE:" "Help should print usage to stdout"

            Expect.stringContains
                help
                "--favorite-color"
                "Help should describe the available options"

            let invalid =
                runDotnet [
                    assemblyPath
                    "--favorite-color"
                ]

            Expect.notEqual invalid.ExitCode 0 "An invalid option should fail"
            Expect.isEmpty invalid.Stdout "An invalid option should not write to stdout"

            Expect.stringContains
                invalid.Stderr
                "--favorite-color"
                "The error should identify the invalid option"

            Expect.stringContains invalid.Stderr "USAGE:" "The error should include usage"

            for exceptionText in
                [
                    "Unhandled exception"
                    "ArguParseException"
                    "   at "
                ] do
                Expect.isFalse
                    (invalid.Stderr.Contains exceptionText)
                    $"An invalid option should not print a CLR exception.\n{invalid.Stderr}"

        check [] "1.0.0.0" "unreleased - unknown"
        check [ "GitHash", "hash text" ] "2.3.4.5" "unreleased - hash text"
        check [ "ReleaseDate", "  release date text  " ] "2.3.4.5" "  release date text   - unknown"

        check
            [
                "ReleaseDate", "  release date text  "
                "GitHash", "hash text"
            ]
            "2.3.4.5"
            "  release date text   - hash text"

    let private runArchiveProcess workingDirectory fileName arguments =
        let startInfo =
            ProcessStartInfo(
                fileName,
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

        startInfo.Environment.["DISABLE_COVERAGE"] <- "true"
        startInfo.Environment.["CI"] <- "false"
        startInfo.Environment["MSBUILDDISABLENODEREUSE"] <- "1"
        let result = ProcessCapture.run (TimeSpan.FromSeconds 30.) startInfo

        {|
            ExitCode = result.ExitCode
            Stdout = result.StandardOutput
            Stderr = result.StandardError
            Output =
                result.StandardOutput
                + result.StandardError
        |}

    let private runArchiveBuild directory =
        let fileName, arguments =
            if OperatingSystem.IsWindows() then
                "cmd.exe",
                [
                    "/c"
                    "build.cmd"
                    "CreatePackages"
                ]
            else
                "bash",
                [
                    "./build.sh"
                    "CreatePackages"
                ]

        runArchiveProcess directory fileName arguments

    let private expectArchiveSuccess
        (result:
            {|
                ExitCode: int
                Stdout: string
                Stderr: string
                Output: string
            |})
        =
        Expect.equal result.ExitCode 0 result.Output

    let private generateArchiveProject directory =
        copyGlobalJson (IO.DirectoryInfo directory)

        runTemplate
            directory
            "-n ArchiveApp --githubUsername TestUser --outputType Console --output \"project with spaces\""

        let projectDirectory = IO.Path.Combine(directory, "project with spaces")
        let applicationDirectory = IO.Path.Combine(projectDirectory, "src", "ArchiveApp")

        IO.File.WriteAllBytes(
            IO.Path.Combine(applicationDirectory, "archive-payload.bin"),
            [|
                0uy
                1uy
                127uy
                128uy
                255uy
            |]
        )

        let projectPath = IO.Path.Combine(applicationDirectory, "ArchiveApp.fsproj")
        let project = XDocument.Load projectPath

        project.Root.Add(
            XElement(
                XName.Get "ItemGroup",
                XElement(
                    XName.Get "Content",
                    XAttribute(XName.Get "Include", "archive-payload.bin"),
                    XElement(XName.Get "CopyToPublishDirectory", "Always"),
                    XElement(XName.Get "TargetPath", "données/深い/payload.bin")
                )
            ),
            XElement(
                XName.Get "Target",
                XAttribute(XName.Get "Name", "AddEmptyArchiveDirectory"),
                XAttribute(XName.Get "AfterTargets", "Publish"),
                XElement(
                    XName.Get "MakeDir",
                    XAttribute(XName.Get "Directories", "$(PublishDir)données/empty")
                )
            )
        )

        project.Save projectPath
        projectDirectory

    let private expectPublicationCleanup directory =
        let temp = IO.Path.Combine(directory, "temp")

        let remaining =
            if IO.Directory.Exists temp then
                IO.Directory.GetDirectories(temp, "publish-*")
            else
                [||]

        Expect.isEmpty remaining "Owned publication workspaces should be removed"

    type private ArchiveMetadata = {
        AssemblyVersion: Version
        ReleaseDate: string
        ReleaseChannel: string
        GitHash: string
    }

    let private expectArchiveMetadata path expected =
        use stream = IO.File.OpenRead path
        use pe = new PEReader(stream)
        let reader = pe.GetMetadataReader()
        let assembly = reader.GetAssemblyDefinition()

        Expect.equal
            assembly.Version
            expected.AssemblyVersion
            "Assembly version should come from the changelog"

        let attributes =
            assembly.GetCustomAttributes()
            |> Seq.choose (fun handle ->
                let attribute = reader.GetCustomAttribute handle

                if attribute.Constructor.Kind = HandleKind.MemberReference then
                    let constructor =
                        reader.GetMemberReference(
                            MemberReferenceHandle.op_Explicit attribute.Constructor
                        )

                    if constructor.Parent.Kind = HandleKind.TypeReference then
                        let attributeType =
                            reader.GetTypeReference(
                                TypeReferenceHandle.op_Explicit constructor.Parent
                            )

                        let name = reader.GetString attributeType.Name
                        let mutable value = reader.GetBlobReader attribute.Value

                        Expect.equal
                            (value.ReadUInt16())
                            1us
                            "Custom attributes should have a valid prolog"

                        match name with
                        | "AssemblyMetadataAttribute" ->
                            Some(value.ReadSerializedString(), value.ReadSerializedString())
                        | "AssemblyFileVersionAttribute"
                        | "AssemblyInformationalVersionAttribute" ->
                            Some(name, value.ReadSerializedString())
                        | _ -> None
                    else
                        None
                else
                    None
            )
            |> Map.ofSeq

        for key, value in
            [
                "ReleaseDate", expected.ReleaseDate
                "ReleaseChannel", expected.ReleaseChannel
                "GitHash", expected.GitHash
                "AssemblyFileVersionAttribute", expected.AssemblyVersion.ToString(3)
            ] do
            Expect.equal
                (Map.tryFind key attributes)
                (Some value)
                $"Published assembly should retain {key}"

        let informationalVersion =
            Map.find "AssemblyInformationalVersionAttribute" attributes

        Expect.isTrue
            (informationalVersion.StartsWith(
                expected.AssemblyVersion.ToString(3),
                StringComparison.Ordinal
            ))
            "Informational version should retain the assembly version"

        Expect.isTrue
            (informationalVersion.Contains expected.GitHash)
            "Informational version should retain the source revision"

    let private readCentralHeaders (bytes: byte array) =
        let uint16 offset = BitConverter.ToUInt16(bytes, offset)
        let uint32 offset = BitConverter.ToUInt32(bytes, offset)

        let ending =
            [
                bytes.Length
                - 22 .. -1 .. max
                    0
                    (bytes.Length
                     - 65557)
            ]
            |> List.find (fun offset ->
                uint32 offset = 0x06054b50u
                && offset
                   + 22
                   + int (
                       uint16 (
                           offset
                           + 20
                       )
                   ) = bytes.Length
            )

        let count =
            int (
                uint16 (
                    ending
                    + 10
                )
            )

        let start =
            int (
                uint32 (
                    ending
                    + 16
                )
            )

        Expect.isLessThan count 65535 "The fixture should use a standard central directory"

        let rec read offset remaining =
            if remaining = 0 then
                Expect.equal
                    offset
                    (start
                     + int (
                         uint32 (
                             ending
                             + 12
                         )
                     ))
                    "Central directory size should match its records"

                []
            else
                Expect.equal
                    (uint32 offset)
                    0x02014b50u
                    "Each central record should have the ZIP signature"

                let nameLength =
                    int (
                        uint16 (
                            offset
                            + 28
                        )
                    )

                let extraLength =
                    int (
                        uint16 (
                            offset
                            + 30
                        )
                    )

                let commentLength =
                    int (
                        uint16 (
                            offset
                            + 32
                        )
                    )

                let next =
                    offset
                    + 46
                    + nameLength
                    + extraLength
                    + commentLength

                Expect.isLessThanOrEqual
                    next
                    ending
                    "Central records should end before the directory trailer"

                let name =
                    Text.Encoding.UTF8.GetString(
                        bytes,
                        offset
                        + 46,
                        nameLength
                    )

                (name, bytes.[offset + 5], offset + 5)
                :: read
                    next
                    (remaining
                     - 1)

        read start count

    let private expectUnixCreators headers =
        Expect.isNonEmpty headers "The ZIP should contain entries"

        for name, creator, _ in headers do
            Expect.equal creator 3uy $"{name} should declare Unix creator platform 3"

    let private expectApphostMachine (runtime: string) (bytes: byte array) =
        use stream = new IO.MemoryStream(bytes, false)
        use reader = new IO.BinaryReader(stream)
        let isArm64 = runtime.EndsWith("-arm64", StringComparison.Ordinal)

        match runtime with
        | "linux-x64"
        | "linux-arm64" ->
            Expect.equal
                (reader.ReadBytes 4)
                [|
                    0x7Fuy
                    0x45uy
                    0x4Cuy
                    0x46uy
                |]
                $"{runtime} should contain an ELF apphost"

            Expect.equal (reader.ReadByte()) 2uy $"{runtime} should use a 64-bit ELF apphost"
            Expect.equal (reader.ReadByte()) 1uy $"{runtime} should use little-endian ELF fields"
            stream.Position <- 18L

            Expect.equal
                (reader.ReadUInt16())
                (if isArm64 then 183us else 62us)
                $"{runtime} should contain the matching ELF machine"

        | "osx-x64"
        | "osx-arm64" ->
            Expect.equal
                (reader.ReadUInt32())
                0xFEEDFACFu
                $"{runtime} should contain a 64-bit Mach-O apphost"

            Expect.equal
                (reader.ReadUInt32())
                (if isArm64 then 0x0100000Cu else 0x01000007u)
                $"{runtime} should contain the matching Mach-O CPU type"

        | "win-x64"
        | "win-arm64" ->
            Expect.equal (reader.ReadUInt16()) 0x5A4Dus $"{runtime} should contain a DOS header"
            stream.Position <- 0x3CL
            let peOffset = reader.ReadUInt32()
            stream.Position <- int64 peOffset
            Expect.equal (reader.ReadUInt32()) 0x00004550u $"{runtime} should contain a PE apphost"

            Expect.equal
                (reader.ReadUInt16())
                (if isArm64 then 0xAA64us else 0x8664us)
                $"{runtime} should contain the matching PE machine"

        | _ -> failtestf "Unsupported apphost runtime identifier: %s" runtime

    let private expectArchiveApphostMachine runtime apphost (bytes: byte array) =
        use stream = new IO.MemoryStream(bytes, false)
        use archive = new ZipArchive(stream, ZipArchiveMode.Read)
        use apphostStream = archive.GetEntry(apphost).Open()
        use apphostBytes = new IO.MemoryStream()
        apphostStream.CopyTo apphostBytes
        expectApphostMachine runtime (apphostBytes.ToArray())

    let private replaceArchiveApphost
        (archiveBytes: byte array)
        apphost
        (apphostBytes: byte array)
        =
        use stream = new IO.MemoryStream()
        stream.Write(archiveBytes, 0, archiveBytes.Length)
        stream.Position <- 0L

        do
            use archive = new ZipArchive(stream, ZipArchiveMode.Update, true)
            use apphostStream = archive.GetEntry(apphost).Open()
            apphostStream.SetLength 0L
            apphostStream.Write(apphostBytes, 0, apphostBytes.Length)

        let bytes = stream.ToArray()

        for _, _, creatorOffset in readCentralHeaders bytes do
            bytes.[creatorOffset] <- 3uy

        bytes

    let private verifyConsoleArchives directory version metadata =
        let product = "ArchiveApp"

        let runtimes = [
            "linux-x64"
            "osx-x64"
            "win-x64"
            "linux-arm64"
            "osx-arm64"
            "win-arm64"
        ]

        let dist = IO.Path.Combine(directory, "dist")

        let expectedNames =
            runtimes
            |> List.map (fun runtime -> $"{product}-{version}-{runtime}.zip")
            |> Set.ofList

        let actualNames =
            IO.Directory.GetFiles(dist)
            |> Seq.map IO.Path.GetFileName
            |> Set.ofSeq

        Expect.equal
            actualNames
            expectedNames
            "CreatePackages should produce exactly six named ZIP archives"

        let project =
            XDocument.Load(
                IO.Path.Combine(
                    directory,
                    "src",
                    product,
                    product
                    + ".fsproj"
                )
            )

        let framework =
            project.Descendants(XName.Get "TargetFramework")
            |> Seq.exactlyOne
            |> _.Value

        let nativeOperatingSystem =
            if OperatingSystem.IsWindows() then
                "win"
            elif OperatingSystem.IsLinux() then
                "linux"
            elif OperatingSystem.IsMacOS() then
                "osx"
            else
                failtest "Archive execution requires Windows, Linux, or macOS"

        Expect.equal
            RuntimeInformation.ProcessArchitecture
            RuntimeInformation.OSArchitecture
            "Archive tests must use a native process rather than architecture emulation"

        let nativeArchitecture =
            match RuntimeInformation.ProcessArchitecture with
            | Architecture.X64 -> "x64"
            | Architecture.Arm64 -> "arm64"
            | architecture ->
                failtestf "Archive execution requires x64 or ARM64, got %A" architecture

        let nativeRuntime = $"{nativeOperatingSystem}-{nativeArchitecture}"

        Expect.contains runtimes nativeRuntime "Archive tests must execute the native artifact"

        for runtime in runtimes do
            let archivePath = IO.Path.Combine(dist, $"{product}-{version}-{runtime}.zip")
            let archiveBytes = IO.File.ReadAllBytes archivePath

            let headers =
                archiveBytes
                |> readCentralHeaders

            expectUnixCreators headers

            use archive =
                ZipFile.OpenRead(IO.Path.Combine(dist, $"{product}-{version}-{runtime}.zip"))

            let apphost =
                if runtime.StartsWith "win-" then
                    product
                    + ".exe"
                else
                    product

            let names =
                archive.Entries
                |> Seq.map _.FullName
                |> Set.ofSeq

            Expect.equal
                headers.Length
                archive.Entries.Count
                "Every entry should have a checked central header"

            let orderedNames =
                headers
                |> List.map (fun (name, _, _) -> name)

            Expect.equal
                orderedNames
                (orderedNames
                 |> List.sortWith (fun left right -> StringComparer.Ordinal.Compare(left, right)))
                "ZIP entries should have stable ordinal path order"

            Expect.equal names.Count archive.Entries.Count "ZIP entry names should be unique"

            Expect.isTrue
                (Set.contains "données/empty/" names)
                "Empty published directories should have ZIP entries"

            use payload = archive.GetEntry("données/深い/payload.bin").Open()
            use payloadBytes = new IO.MemoryStream()
            payload.CopyTo payloadBytes

            Expect.equal
                (payloadBytes.ToArray())
                [|
                    0uy
                    1uy
                    127uy
                    128uy
                    255uy
                |]
                "Nested UTF-8 paths should retain the exact published payload"

            for name in
                [
                    apphost
                    product
                    + ".dll"
                    product
                    + ".deps.json"
                    product
                    + ".runtimeconfig.json"
                    "FSharp.Core.dll"
                    "Argu.dll"
                ] do
                Expect.isTrue
                    (Set.contains name names)
                    $"{runtime} should contain {name} at its root"

            Expect.isFalse
                (Set.contains "coreclr.dll" names
                 || Set.contains "libcoreclr.so" names
                 || Set.contains "libcoreclr.dylib" names)
                "Framework-dependent archives should not include the runtime"

            expectArchiveApphostMachine runtime apphost archiveBytes

            if runtime.EndsWith("-arm64", StringComparison.Ordinal) then
                let x64Runtime = runtime.Replace("-arm64", "-x64")

                use x64Archive =
                    ZipFile.OpenRead(IO.Path.Combine(dist, $"{product}-{version}-{x64Runtime}.zip"))

                use x64Apphost = x64Archive.GetEntry(apphost).Open()
                use x64Bytes = new IO.MemoryStream()
                x64Apphost.CopyTo x64Bytes

                let controlBytes = replaceArchiveApphost archiveBytes apphost (x64Bytes.ToArray())
                use controlStream = new IO.MemoryStream(controlBytes, false)
                use controlArchive = new ZipArchive(controlStream, ZipArchiveMode.Read)

                for metadataName in
                    [
                        $"{product}.deps.json"
                        $"{product}.runtimeconfig.json"
                    ] do
                    use originalMetadata =
                        new IO.StreamReader(archive.GetEntry(metadataName).Open())

                    use controlMetadata =
                        new IO.StreamReader(controlArchive.GetEntry(metadataName).Open())

                    Expect.equal
                        (controlMetadata.ReadToEnd())
                        (originalMetadata.ReadToEnd())
                        $"{runtime} control should retain its correct ARM64 manifest and framework"

                let failure =
                    try
                        expectArchiveApphostMachine runtime apphost controlBytes
                        None
                    with error ->
                        Some error.Message

                match failure with
                | Some message ->
                    Expect.isTrue
                        (message.Contains(
                            $"{runtime} should contain the matching",
                            StringComparison.Ordinal
                        ))
                        $"The x64 substitution should fail the {runtime} machine assertion: {message}"
                | None -> failtestf "%s should reject an x64 apphost inside its ZIP" runtime

            let assemblyEntry =
                archive.GetEntry(
                    product
                    + ".dll"
                )

            Expect.isLessThan
                assemblyEntry.CompressedLength
                assemblyEntry.Length
                "Published assemblies should use ZIP compression"

            use configurationStream =
                archive
                    .GetEntry(
                        product
                        + ".runtimeconfig.json"
                    )
                    .Open()

            use configuration = JsonDocument.Parse configurationStream
            let options = configuration.RootElement.GetProperty "runtimeOptions"

            Expect.equal
                (options.GetProperty("tfm").GetString())
                framework
                "The application project should determine the archived framework"

            let runtimeFramework = options.GetProperty "framework"

            Expect.equal
                (runtimeFramework.GetProperty("name").GetString())
                "Microsoft.NETCore.App"
                "The application should use the shared framework"

            Expect.equal
                (runtimeFramework.GetProperty("version").GetString())
                (framework.Substring(3)
                 + ".0")
                "The shared framework version should match the application project"

            Expect.isFalse
                (fst (options.TryGetProperty "includedFrameworks"))
                "The runtime configuration should be framework-dependent"

            use dependenciesStream = archive.GetEntry($"{product}.deps.json").Open()
            use dependencies = JsonDocument.Parse dependenciesStream

            Expect.equal
                (dependencies.RootElement
                    .GetProperty("runtimeTarget")
                    .GetProperty("name")
                    .GetString())
                $".NETCoreApp,Version=v{framework.Substring(3)}/{runtime}"
                "The dependency manifest should match the archive RID and project framework"

            use extracted = Disposables.DisposableDirectory.Create()
            archive.ExtractToDirectory extracted.Directory

            use standardExtracted = Disposables.DisposableDirectory.Create()

            if not (OperatingSystem.IsWindows()) then
                runArchiveProcess directory "unzip" [
                    "-q"
                    archivePath
                    "-d"
                    standardExtracted.Directory
                ]

                |> expectArchiveSuccess

            for extractedDirectory in
                [
                    extracted.Directory
                    if not (OperatingSystem.IsWindows()) then
                        standardExtracted.Directory
                ] do
                let emptyDirectory = IO.Path.Combine(extractedDirectory, "données", "empty")

                Expect.isTrue
                    (IO.Directory.Exists emptyDirectory)
                    "Fresh extraction should retain empty directories"

                Expect.isEmpty
                    (IO.Directory.GetFileSystemEntries emptyDirectory)
                    "The published empty directory should remain empty"

            for entry in archive.Entries do
                Expect.isFalse
                    (entry.FullName.Contains '\\'
                     || entry.FullName.StartsWith "/"
                     || (entry.FullName.Split('/')
                         |> Array.contains ".."))
                    "ZIP entries should be relative slash-separated paths"

                let isDirectory = entry.FullName.EndsWith "/"

                let expectedAttributes =
                    if isDirectory then
                        0x41ED0010
                    elif
                        not (runtime.StartsWith "win-")
                        && entry.FullName = product
                    then
                        int 0x81ED0000u
                    else
                        int 0x81A40000u

                Expect.equal
                    entry.ExternalAttributes
                    expectedAttributes
                    $"{runtime}/{entry.FullName} should retain its complete Unix file type and permissions"

                if not isDirectory then
                    let extractedPath = IO.Path.Combine(extracted.Directory, entry.FullName)
                    use original = entry.Open()
                    use contents = new IO.MemoryStream()
                    original.CopyTo contents

                    Expect.equal
                        (IO.File.ReadAllBytes extractedPath)
                        (contents.ToArray())
                        $"{runtime}/{entry.FullName} should extract without changes"

                    if not (OperatingSystem.IsWindows()) then
                        Expect.equal
                            (IO.File.ReadAllBytes(
                                IO.Path.Combine(standardExtracted.Directory, entry.FullName)
                            ))
                            (contents.ToArray())
                            $"{runtime}/{entry.FullName} should retain its bytes after standard extraction"

            metadata
            |> Option.iter (
                expectArchiveMetadata (
                    IO.Path.Combine(
                        extracted.Directory,
                        product
                        + ".dll"
                    )
                )
            )

            if runtime = nativeRuntime then
                let executable = IO.Path.Combine(extracted.Directory, apphost)

                if not (OperatingSystem.IsWindows()) then
                    Expect.equal
                        (IO.File.GetUnixFileMode executable)
                        (enum<IO.UnixFileMode> 0o755)
                        "Fresh extraction should preserve apphost executable permissions"

                let result = runArchiveProcess extracted.Directory executable [ "--version" ]
                expectArchiveSuccess result

                let expectedVersion =
                    metadata
                    |> Option.map (fun value -> string value.AssemblyVersion)
                    |> Option.defaultValue "0.1.0.0"

                Expect.equal
                    (result.Output.Trim())
                    expectedVersion
                    "The extracted native apphost should report the changelog version"

                let standardExecutable = IO.Path.Combine(standardExtracted.Directory, apphost)

                for extractedDirectory, extractedExecutable in
                    [
                        extracted.Directory, executable
                        if not (OperatingSystem.IsWindows()) then
                            standardExtracted.Directory, standardExecutable
                    ] do
                    let help = runArchiveProcess extractedDirectory extractedExecutable [ "--help" ]
                    expectArchiveSuccess help

                    Expect.isEmpty
                        help.Stderr
                        $"Native help should not write to stderr.\n{help.Stderr}"

                    Expect.stringContains
                        help.Stdout
                        "USAGE:"
                        "Native help should print usage to stdout"

                    Expect.stringContains
                        help.Stdout
                        "--favorite-color"
                        "Native help should describe the available options"

                if not (OperatingSystem.IsWindows()) then
                    Expect.isTrue
                        (IO.File.GetUnixFileMode(standardExecutable).HasFlag
                            IO.UnixFileMode.UserExecute)
                        "Standard extraction should preserve apphost execute permission without chmod"

                    let standardVersion =
                        runArchiveProcess standardExtracted.Directory standardExecutable [
                            "--version"
                        ]

                    expectArchiveSuccess standardVersion

                    Expect.equal
                        (standardVersion.Output.Trim())
                        expectedVersion
                        "The standard-extracted native apphost should report the changelog version"

                metadata
                |> Option.iter (fun expected ->
                    let info = runArchiveProcess extracted.Directory executable [ "--info" ]
                    expectArchiveSuccess info

                    Expect.equal
                        (info.Output.Trim())
                        $"{product} - {expected.AssemblyVersion} - {expected.ReleaseDate} - {expected.GitHash}"
                        "The extracted apphost should retain release metadata"

                    if not (OperatingSystem.IsWindows()) then
                        let standardInfo =
                            runArchiveProcess standardExtracted.Directory standardExecutable [
                                "--info"
                            ]

                        expectArchiveSuccess standardInfo

                        Expect.equal
                            (standardInfo.Output.Trim())
                            $"{product} - {expected.AssemblyVersion} - {expected.ReleaseDate} - {expected.GitHash}"
                            "The standard-extracted apphost should retain release metadata"
                )

        expectPublicationCleanup directory

    [<Tests>]
    let consoleArchiveTests =
        testSequenced
        <| testList "console archives" [
            testCase "Raw creator check rejects creator zero with Unix executable attributes"
            <| fun _ ->
                use stream = new IO.MemoryStream()

                do
                    use archive = new ZipArchive(stream, ZipArchiveMode.Create, true)
                    let entry = archive.CreateEntry "app"
                    entry.ExternalAttributes <- int 0x81ED0000u
                    use payload = entry.Open()
                    payload.WriteByte 42uy

                let bytes = stream.ToArray()

                let _, _, creatorOffset =
                    readCentralHeaders bytes
                    |> List.exactlyOne

                bytes.[creatorOffset] <- 3uy

                readCentralHeaders bytes
                |> expectUnixCreators

                bytes.[creatorOffset] <- 0uy
                let headers = readCentralHeaders bytes
                use negativeStream = new IO.MemoryStream(bytes)
                use negativeArchive = new ZipArchive(negativeStream, ZipArchiveMode.Read)

                Expect.equal
                    negativeArchive.Entries.[0].ExternalAttributes
                    (int 0x81ED0000u)
                    "The negative control should retain Unix executable attributes"

                Expect.throws
                    (fun () -> expectUnixCreators headers)
                    "Creator zero should fail the creator-platform check on every host"

            testCase
                "CreatePackages publishes framework-dependent root ZIPs and executable apphosts"
            <| fun _ ->
                use generated = Disposables.DisposableDirectory.Create()
                let directory = generateArchiveProject generated.Directory

                runArchiveBuild directory
                |> expectArchiveSuccess

                verifyConsoleArchives directory "0.1.0" None

            testCase "CreatePackages retains dated prerelease metadata and project framework"
            <| fun _ ->
                use generated = Disposables.DisposableDirectory.Create()
                let directory = generateArchiveProject generated.Directory

                let projectPath =
                    IO.Path.Combine(directory, "src", "ArchiveApp", "ArchiveApp.fsproj")

                let project = XDocument.Load projectPath

                project.Descendants(XName.Get "TargetFramework")
                |> Seq.exactlyOne
                |> fun element -> element.Value <- "net9.0"

                project.Save projectPath

                IO.File.WriteAllText(
                    IO.Path.Combine(directory, "CHANGELOG.md"),
                    "# Changelog\n\n## [Unreleased]\n\n## [2.3.4-beta.1] - 2024-02-03\n\n### Added\n- Archive regression fixture.\n"
                )

                for arguments in
                    [
                        [ "init" ]
                        [
                            "config"
                            "user.name"
                            "Template Test"
                        ]
                        [
                            "config"
                            "user.email"
                            "template-test@example.invalid"
                        ]
                        [
                            "add"
                            "."
                        ]
                        [
                            "commit"
                            "-m"
                            "Archive fixture"
                        ]
                    ] do
                    runArchiveProcess directory "git" arguments
                    |> expectArchiveSuccess

                let revision =
                    runArchiveProcess directory "git" [
                        "rev-parse"
                        "HEAD"
                    ]

                expectArchiveSuccess revision

                runArchiveBuild directory
                |> expectArchiveSuccess

                verifyConsoleArchives
                    directory
                    "2.3.4-beta.1"
                    (Some {
                        AssemblyVersion = System.Version(2, 3, 4, 0)
                        ReleaseDate = "2024-02-03T00:00:00.0000000"
                        ReleaseChannel = "beta"
                        GitHash = revision.Output.Trim()
                    })

            testCase "CreatePackages removes its workspace after a real publish failure"
            <| fun _ ->
                use generated = Disposables.DisposableDirectory.Create()
                let directory = generateArchiveProject generated.Directory

                let projectPath =
                    IO.Path.Combine(directory, "src", "ArchiveApp", "ArchiveApp.fsproj")

                let project = XDocument.Load projectPath

                project.Root.Add(
                    XElement(
                        XName.Get "Target",
                        XAttribute(XName.Get "Name", "FailArchivePublication"),
                        XAttribute(XName.Get "BeforeTargets", "Publish"),
                        XElement(
                            XName.Get "WriteLinesToFile",
                            XAttribute(
                                XName.Get "File",
                                "$(MSBuildProjectDirectory)/../../publish-marker.txt"
                            ),
                            XAttribute(XName.Get "Lines", "$(PublishDir)"),
                            XAttribute(XName.Get "Overwrite", "true")
                        ),
                        XElement(
                            XName.Get "Error",
                            XAttribute(XName.Get "Text", "Archive publication failure fixture")
                        )
                    )
                )

                project.Save projectPath
                let result = runArchiveBuild directory
                Expect.notEqual result.ExitCode 0 "The publication error should fail CreatePackages"

                Expect.isTrue
                    (result.Output.Contains "Archive publication failure fixture")
                    result.Output

                let marker = IO.Path.Combine(directory, "publish-marker.txt")

                Expect.isTrue
                    (IO.File.Exists marker)
                    "The failure must occur inside MSBuild publication"

                let publishDirectory = IO.File.ReadAllText(marker).Trim()

                Expect.isTrue
                    (publishDirectory.Contains "publish-")
                    $"The marker should identify the owned publication workspace: {publishDirectory}"

                Expect.isFalse
                    (IO.Directory.Exists publishDirectory)
                    "The failed publication directory should be removed"

                expectPublicationCleanup directory
        ]

    [<Tests>]
    let metadataTests =
        testList "metadata" [
            testCase "packaged parameters are explicitly visible in IDE dialogs"
            <| fun _ ->
                use archive = ZipFile.OpenRead nugetPkgPath

                use templateStream =
                    archive.GetEntry("Content/.template.config/template.json").Open()

                use template = JsonDocument.Parse templateStream
                use hostStream = archive.GetEntry("Content/.template.config/ide.host.json").Open()
                use host = JsonDocument.Parse hostStream

                let parameterIds =
                    template.RootElement.GetProperty("symbols").EnumerateObject()
                    |> Seq.filter (fun symbol ->
                        symbol.Value.GetProperty("type").GetString() = "parameter"
                    )
                    |> Seq.map (fun symbol -> symbol.Name)
                    |> Set.ofSeq

                for id in
                    [
                        "githubUsername"
                        "outputType"
                        "releaseBranch"
                    ] do
                    Expect.isTrue
                        (Set.contains id parameterIds)
                        $"The packaged template should declare the {id} parameter"

                let hostSymbols =
                    host.RootElement.GetProperty("symbolInfo").EnumerateArray()
                    |> Seq.toList

                for id in parameterIds do
                    let entries =
                        hostSymbols
                        |> List.filter (fun symbol -> symbol.GetProperty("id").GetString() = id)

                    Expect.equal entries.Length 1 $"The IDE host should declare {id} exactly once"
                    let entry = List.exactlyOne entries

                    match entry.TryGetProperty "isVisible" with
                    | true, visibility ->
                        Expect.equal
                            visibility.ValueKind
                            JsonValueKind.True
                            $"The IDE host should explicitly set isVisible to true for {id}"
                    | false, _ ->
                        failtestf "The IDE host should explicitly declare isVisible for %s" id

            testCase "packaged IDE label uses GitHub Username spelling"
            <| fun _ ->
                use archive = ZipFile.OpenRead nugetPkgPath
                use hostStream = archive.GetEntry("Content/.template.config/ide.host.json").Open()
                use host = JsonDocument.Parse hostStream

                let githubUsername =
                    host.RootElement.GetProperty("symbolInfo").EnumerateArray()
                    |> Seq.filter (fun symbol ->
                        symbol.GetProperty("id").GetString() = "githubUsername"
                    )
                    |> Seq.exactlyOne

                Expect.equal
                    (githubUsername.GetProperty("name").GetProperty("text").GetString())
                    "GitHub Username"
                    "The IDE label should use GitHub capitalization"
        ]

    [<Tests>]
    let tests =
        testSequenced
        <| // uncomment to get better logs
        testList "samples" [
            do setup ()
            testList "contracts" [
                testCase
                    "console commands preserve optional release metadata"
                    consoleCommandsPreserveOptionalMetadata
                testCase
                    "packed libraries supply FSharp.Core to clean CSharp consumers in Library solutions"
                    (packedLibrariesSupplyFSharpCore Library)
                testCase
                    "packed libraries supply FSharp.Core to clean CSharp consumers in Console solutions"
                    (packedLibrariesSupplyFSharpCore Console)
            ]
            testList
                "generated builds"
                ([
                    Library,
                    testCase,
                    "-n MyCoolLib --githubUsername CoolPersonNo2",
                    [
                        yield! projectStructureAsserts
                        Assert.``File does not exist`` "src/MyCoolLib/AssemblyInfo.fs"
                        Assert.``project can build target`` "DotnetPack"
                        Assert.``assembly info values are set after pack`` "MyCoolLib"
                        Assert.``project can build target`` "BuildDocs"
                    ]


                    Library,
                    testCase,
                    "-n ProjLibTest --githubUsername CoolPersonNo0",
                    [
                        yield! projectStructureAsserts
                        Assert.``project can build target`` "DotnetPack"
                        Effect.``dotnet new``
                            "mini-scaffold -n MyCoolLib3 --githubUsername CoolPersonNo3 --outputType projLib"
                            "src"
                        Effect.``dotnet sln add`` "src/MyCoolLib3/MyCoolLib3.fsproj"
                        Assert.``File exists`` "src/MyCoolLib3/MyCoolLib3.fsproj"
                        Assert.``File does not exist`` "src/MyCoolLib3/AssemblyInfo.fs"
                        Assert.``project can build target`` "DotnetPack"
                    ]

                    Library,
                    testCase,
                    "-n ProjConsoleTest --githubUsername CoolPersonNo0",
                    [
                        yield! projectStructureAsserts
                        Assert.``project can build target`` "DotnetPack"
                        Effect.``dotnet new``
                            "mini-scaffold -n MyCoolConsole --githubUsername CoolPersonNo3 --outputType projConsole"
                            "src"
                        Effect.``dotnet sln add`` "src/MyCoolConsole/MyCoolConsole.fsproj"
                        Assert.``File exists`` "src/MyCoolConsole/MyCoolConsole.fsproj"
                        Assert.``File does not exist`` "src/MyCoolConsole/AssemblyInfo.fs"
                        Assert.``project can build target`` "DotnetPack"
                        Effect.``dotnet run`` "" "src/MyCoolConsole/"
                    ]


                    Library,
                    testCase,
                    "-n ProjTestTest --githubUsername CoolPersonNo0",
                    [
                        yield! projectStructureAsserts
                        Assert.``project can build target`` "DotnetPack"
                        Effect.``dotnet new``
                            "mini-scaffold -n MyCoolLib3 --githubUsername CoolPersonNo3 --outputType projLib"
                            "src"
                        Effect.``dotnet sln add`` "src/MyCoolLib3/MyCoolLib3.fsproj"
                        Effect.``dotnet new``
                            "mini-scaffold -n MyCoolLib3.Tests --githubUsername CoolPersonNo3 --outputType projTest"
                            "tests"
                        Effect.``dotnet sln add`` "tests/MyCoolLib3.Tests/MyCoolLib3.Tests.fsproj"
                        Effect.``dotnet add reference``
                            "../../src/MyCoolLib3/MyCoolLib3.fsproj"
                            "tests/MyCoolLib3.Tests/"
                        Assert.``File exists`` "tests/MyCoolLib3.Tests/MyCoolLib3.Tests.fsproj"
                        Assert.``File does not exist`` "tests/MyCoolLib3.Tests/AssemblyInfo.fs"
                        Assert.``project can build target`` "DotnetPack"
                    ]

                    // test for dashes in name https://github.com/dotnet/templating/issues/1168#issuecomment-364592031
                    Library,
                    testCase,
                    "-n fsharp-data-sample --githubUsername CoolPersonNo2",
                    [
                        yield! projectStructureAsserts
                        Assert.``project can build target`` "DotnetPack"
                    ]
                    Console,
                    testCase,
                    "-n MyCoolApp --githubUsername CoolPersonNo2 --outputType Console",
                    [
                        yield! projectStructureAsserts
                        Assert.``project can build target`` "CreatePackages"
                    ]
                    // Test that CHANGELOG.md is not modified during build failures,
                    // *unless* at least one step has pushed a release to the outside world.

                    Library,
                    testCase,
                    "-n AssemblyInfoFail --githubUsername TestAccount",
                    [
                        Effect.``setup for release tests``
                        Effect.``make build function fail`` "let updateChangelog"
                        Assert.``CHANGELOG contains Unreleased section``
                        Assert.``build target with failure expected`` "Release"
                        Assert.``CHANGELOG contains Unreleased section``
                    ]
                    Library,
                    testCase,
                    "-n GitReleaseFailBeforeCommit --githubUsername TestAccount",
                    [
                        Effect.``setup for release tests``
                        Effect.``make failure in gitRelease function`` "Git.Commit.exec"
                        Assert.``CHANGELOG contains Unreleased section``
                        Assert.``build target with failure expected`` "Release"
                        Assert.``CHANGELOG contains Unreleased section``
                    ]
                    Library,
                    testCase,
                    "-n GitReleaseFailAfterCommit --githubUsername TestAccount",
                    [
                        Effect.``setup for release tests``
                        Effect.``make failure in gitRelease function`` "Git.Branches.push \"\""
                        Assert.``CHANGELOG contains Unreleased section``
                        Assert.``build target with failure expected`` "Release"
                        Assert.``CHANGELOG does not contain Unreleased section``
                    ]
                    Library,
                    testCase,
                    "-n GitReleaseSuccess --githubUsername TestAccount",
                    [
                        Effect.``setup for release tests``
                        Assert.``CHANGELOG contains Unreleased section``
                        Assert.``project can build target`` "Release"
                        Assert.``CHANGELOG does not contain Unreleased section``
                    ]

                    Library,
                    testCase,
                    "-n DotnetRestoreFail --githubUsername TestAccount",
                    [
                        Effect.``setup for publish tests``
                        Effect.``make build function fail`` "let dotnetRestore"
                        Assert.``build target with failure expected`` "Publish"
                    ]
                    Library,
                    testCase,
                    "-n DotnetBuildFail --githubUsername TestAccount",
                    [
                        Effect.``setup for publish tests``
                        Effect.``make build function fail`` "let dotnetBuild"
                        Assert.``build target with failure expected`` "Publish"
                    ]
                    Library,
                    testCase,
                    "-n DotnetTestFail --githubUsername TestAccount",
                    [
                        Effect.``setup for publish tests``
                        Effect.``make build function fail`` "let dotnetTest"
                        Assert.``build target with failure expected`` "Publish"
                    ]
                    Library,
                    testCase,
                    "-n CoverageReportFail --githubUsername TestAccount",
                    [
                        Effect.``setup for publish tests``
                        Effect.``make build function fail`` "let generateCoverageReport"
                        Assert.``build target with failure expected`` "Publish"
                    ]
                    Library,
                    testCase,
                    "-n DotnetPackFail --githubUsername TestAccount",
                    [
                        Effect.``setup for publish tests``
                        Effect.``make build function fail`` "let dotnetPack"
                        Assert.``build target with failure expected`` "Publish"
                    ]

                    Library,
                    testCase,
                    "-n PublishToNugetFail --githubUsername TestAccount",
                    [
                        Effect.``setup for publish tests``
                        Effect.``make build function fail`` "let publishToNuget"
                        Assert.``build target with failure expected`` "Publish"
                    ]
                    Library,
                    testCase,
                    "-n PublishToNugetSuccess --githubUsername TestAccount",
                    [
                        Effect.``setup for publish tests``
                        Assert.``project can build target`` "PublishToNuget"
                    ]

                    Library,
                    testCase,
                    "-n GitHubReleaseFail --githubUsername TestAccount",
                    [
                        Effect.``setup for publish tests``
                        Effect.``make build function fail`` "githubRelease"
                        Assert.``build target with failure expected`` "GitHubRelease"
                    ]

                    Console,
                    testCase,
                    "-n DotnetRestoreFail --githubUsername TestAccount --outputType Console",
                    [
                        Effect.``setup for release tests``
                        Effect.``make build function fail`` "let dotnetRestore"
                        Assert.``CHANGELOG contains Unreleased section``
                        Assert.``build target with failure expected`` "Release"
                        Assert.``CHANGELOG contains Unreleased section``
                    ]
                    Console,
                    testCase,
                    "-n AssemblyInfoFail --githubUsername TestAccount --outputType Console",
                    [
                        Effect.``setup for release tests``
                        Effect.``make build function fail`` "let updateChangelog"
                        Assert.``CHANGELOG contains Unreleased section``
                        Assert.``build target with failure expected`` "Release"
                        Assert.``CHANGELOG contains Unreleased section``
                    ]
                    Console,
                    testCase,
                    "-n DotnetBuildFail --githubUsername TestAccount --outputType Console",
                    [
                        Effect.``setup for release tests``
                        Effect.``make build function fail`` "let dotnetBuild"
                        Assert.``CHANGELOG contains Unreleased section``
                        Assert.``build target with failure expected`` "Release"
                        Assert.``CHANGELOG contains Unreleased section``
                    ]
                    Console,
                    testCase,
                    "-n DotnetTestFail --githubUsername TestAccount --outputType Console",
                    [
                        Effect.``setup for release tests``
                        Effect.``make build function fail`` "let dotnetTest"
                        Assert.``CHANGELOG contains Unreleased section``
                        Assert.``build target with failure expected`` "Release"
                        Assert.``CHANGELOG contains Unreleased section``
                    ]
                    Console,
                    testCase,
                    "-n CoverageReportFail --githubUsername TestAccount --outputType Console",
                    [
                        Effect.``setup for release tests``
                        Effect.``make build function fail`` "let generateCoverageReport"
                        Assert.``CHANGELOG contains Unreleased section``
                        Assert.``build target with failure expected`` "Release"
                        Assert.``CHANGELOG contains Unreleased section``
                    ]
                    Console,
                    testCase,
                    "-n CreatePackagesFail --githubUsername TestAccount --outputType Console",
                    [
                        Effect.``setup for release tests``
                        Effect.``make build function fail`` "let createPackages"
                        Assert.``CHANGELOG contains Unreleased section``
                        Assert.``build target with failure expected`` "Release"
                        Assert.``CHANGELOG contains Unreleased section``
                    ]
                    Console,
                    testCase,
                    "-n GitReleaseFail --githubUsername TestAccount --outputType Console",
                    [
                        Effect.``setup for release tests``
                        Effect.``make build function fail`` "let gitRelease"
                        Assert.``CHANGELOG contains Unreleased section``
                        Assert.``build target with failure expected`` "Release"
                        Assert.``CHANGELOG contains Unreleased section``
                    ]
                    Console,
                    testCase,
                    "-n GitHubReleaseFail --githubUsername TestAccount --outputType Console",
                    [
                        Effect.``setup for release tests``
                        Effect.``make build function fail`` "let githubRelease"
                        Effect.``disable pushing in gitRelease function``
                        Assert.``CHANGELOG contains Unreleased section``
                        Assert.``build target with failure expected`` "Release"
                        // Since the GitRelease step "succeeded", we no longer revert the changelog from that point on
                        Assert.``CHANGELOG does not contain Unreleased section``
                    ]

                    // Try to run a release on an alternate release branch name.
                    Library,
                    testCase,
                    "-n AlternateReleaseBranch --githubUsername TestAccount --releaseBranch alternateBranch",
                    [
                        Effect.``setup for release branch tests`` "alternateBranch"
                        Assert.``project can build target`` "Release"
                    ]

                    // This should fail since the release branch specified in the build script doesn't match the
                    // branch from which the script is executed.
                    Library,
                    testCase,
                    "-n ReleaseBranchMissingFailure --githubUsername TestAccount --releaseBranch notExist",
                    [
                        Effect.``setup for release branch tests`` "anotherBranch"
                        Assert.``build target with failure expected`` "Release"
                    ]

                    Library,
                    testCase,
                    "-n ChangelogTagLinks --githubUsername TestAccount",
                    [
                        Effect.``setup for release tests``
                        Effect.``set environment variable`` "RELEASE_VERSION" "0.2.0"
                        Assert.``project can build target`` "Release"
                        Assert.``CHANGELOG links compare v-prefixed release tags``
                            "TestAccount"
                            "ChangelogTagLinks"
                            "0.1.0"
                            "0.2.0"
                    ]

                    Console,
                    testCase,
                    "-n ChangelogTagLinks --githubUsername TestAccount --outputType Console",
                    [
                        Effect.``setup for release tests``
                        Effect.``set environment variable`` "RELEASE_VERSION" "0.2.0"
                        Effect.``change githubRelease function to only run release checks``
                        Assert.``project can build target`` "Release"
                        Assert.``CHANGELOG links compare v-prefixed release tags``
                            "TestAccount"
                            "ChangelogTagLinks"
                            "0.1.0"
                            "0.2.0"
                    ]

                 ]
                 |> List.mapFold
                     (fun libraryIndex (host, testCase, args, additionalAsserts) ->
                         let tests = [
                             testCase args
                             <| fun _ ->
                                 use d = Disposables.DisposableDirectory.Create()
                                 copyGlobalJson d.DirectoryInfo

                                 showTemplateHelp
                                 <| Some d.Directory

                                 runTemplate d.Directory args

                                 // The project we just generated is the only one in here
                                 let projectDir =
                                     d.DirectoryInfo.GetDirectories()
                                     |> Seq.head

                                 additionalAsserts
                                 |> Seq.iter (fun asserter -> asserter projectDir)
                         ]

                         match host with
                         | Library ->
                             let group = if libraryIndex % 2 = 0 then "A" else "B"

                             testList "Library" [ testList group tests ],
                             libraryIndex
                             + 1
                         | Console -> testList "Console" tests, libraryIndex
                     )
                     0
                 |> fst)

        ]
