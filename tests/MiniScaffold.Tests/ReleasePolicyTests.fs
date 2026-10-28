namespace MiniScaffold.Tests

module ReleasePolicyTests =
    open System
    open System.Diagnostics
    open System.IO
    open Expecto
    open Infrastructure

    type private ProcessResult = { ExitCode: int; Output: string }

    type private ExpectedOutcome =
        | Intercepted
        | Rejected of string

    type private Scenario = {
        Name: string
        CI: string option
        Tag: string option
        Version: string
        HasChanges: bool
        Detached: bool
        Expected: ExpectedOutcome
    }

    let private sourceRoot =
        Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "../.."))

    let private run directory executable arguments environment =
        let startInfo = ProcessStartInfo(executable)
        startInfo.WorkingDirectory <- directory
        startInfo.UseShellExecute <- false
        startInfo.RedirectStandardOutput <- true
        startInfo.RedirectStandardError <- true

        arguments
        |> List.iter startInfo.ArgumentList.Add

        startInfo.Environment.Keys
        |> Seq.filter (fun key ->
            key.StartsWith("GIT_", StringComparison.OrdinalIgnoreCase)
            || List.contains key [
                "CI"
                "RELEASE_TAG"
                "GITHUB_TOKEN"
                "NUGET_TOKEN"
                "GITHUB_ACTIONS"
                "MSBuildSDKsPath"
                "MSBuildExtensionsPath"
            ]
        )
        |> Seq.toArray
        |> Array.iter (fun key ->
            startInfo.Environment.Remove key
            |> ignore
        )

        environment
        |> List.iter (fun (key, value) -> startInfo.Environment[key] <- value)

        use child = new Process(StartInfo = startInfo)
        Expect.isTrue (child.Start()) $"Could not start {executable}."
        let stdout = child.StandardOutput.ReadToEndAsync()
        let stderr = child.StandardError.ReadToEndAsync()
        child.WaitForExit()

        {
            ExitCode = child.ExitCode
            Output =
                stdout.GetAwaiter().GetResult()
                + stderr.GetAwaiter().GetResult()
        }

    let private checkedRun directory executable arguments =
        let result = run directory executable arguments []

        Expect.equal
            result.ExitCode
            0
            $"{executable} {arguments} failed in {directory}.\n{result.Output}"

        result.Output.Trim()

    let private replaceOnce (original: string) (replacement: string) (source: string) =
        let position = source.IndexOf(original, StringComparison.Ordinal)
        Expect.isGreaterThanOrEqual position 0 $"Missing instrumentation anchor: {original}"

        Expect.equal
            (source.LastIndexOf(original, StringComparison.Ordinal))
            position
            $"Ambiguous instrumentation anchor: {original}"

        source.Substring(0, position)
        + replacement
        + source.Substring(
            position
            + original.Length
        )

    let private instrument (source: string) =
        let lineEndings (text: string) =
            let normalized = text.Replace("\r\n", "\n")

            if source.Contains "\r\n" then
                normalized.Replace("\n", "\r\n")
            else
                normalized

        let nugetPrefix =
            lineEndings "let publishToNuget _ =\n    allPublishChecks ()\n\n    "

        let githubPrefix =
            """let githubRelease _ =
    allPublishChecks ()

    let token =
        match githubToken with
        | Some s -> s
        | _ ->
            failwith
                "please set the github_token environment variable to a github personal access token with repo access."

    let files = !!distGlob
    // Get release notes with properly-linked version number

    let releaseNotes = Changelog.mkReleaseNotes changelog latestEntry gitHubRepoUrl

    """

        let helper =
            """exception PublicationIntercepted of string

let private interceptPublication boundary (_callee: 'a) : 'a =
    System.IO.File.AppendAllText("attempts.txt", boundary + "\n")
    raise (PublicationIntercepted boundary)

"""

        let substitutions = [
            "DotNet.nugetPush", "(interceptPublication \"NuGet\" DotNet.nugetPush)"
            "GitHub.createClientWithToken",
            "(interceptPublication \"GitHub\" GitHub.createClientWithToken)"
        ]

        for prefix, callee in
            [
                nugetPrefix, "DotNet.nugetPush"
                githubPrefix, "GitHub.createClientWithToken"
            ] do
            let anchor =
                lineEndings prefix
                + callee

            replaceOnce anchor anchor source
            |> ignore

        let transformed =
            substitutions
            |> List.fold
                (fun text (original, replacement) -> replaceOnce original replacement text)
                source
            |> replaceOnce
                nugetPrefix
                (helper
                 + nugetPrefix)

        let restored =
            substitutions
            |> List.fold
                (fun text (original, replacement) -> replaceOnce replacement original text)
                transformed
            |> replaceOnce helper ""

        Expect.equal
            restored
            source
            "Instrumentation changed code outside the two callees and test helper."

        transformed

    let private fixture =
        lazy
            let directory = Disposables.DisposableDirectory.Create()
            let root = directory.Directory

            Directory.CreateDirectory(Path.Combine(root, "build"))
            |> ignore

            for relative in
                [
                    "global.json"
                    "Directory.Build.props"
                    "Directory.Packages.props"
                    "build/build.fsproj"
                    "build/Changelog.fs"
                    "build/FsDocs.fs"
                ] do
                File.Copy(Path.Combine(sourceRoot, relative), Path.Combine(root, relative))

            let source = File.ReadAllText(Path.Combine(sourceRoot, "build/build.fs"))

            let transformed = instrument source
            let buildSource = Path.Combine(root, "build/build.fs")
            File.WriteAllText(buildSource, transformed)

            checkedRun root "dotnet" [
                "build"
                "build/build.fsproj"
                "--configuration"
                "Debug"
                "--nologo"
                "-p:ImportDirectoryBuildTargets=false"
            ]
            |> ignore

            Expect.equal
                (File.ReadAllText buildSource)
                transformed
                "The disposable build changed instrumented source."

            AppDomain.CurrentDomain.ProcessExit.Add(fun _ -> Disposables.dispose directory)
            root

    let private changelog version hasChanges =
        let changes =
            if hasChanges then
                "### Changed\n- Release policy fixture.\n"
            else
                ""

        $"# Changelog\n\n## [{version}] - 2026-01-02\n\n{changes}\n## [7.0.0] - 2025-01-01\n\n### Added\n- Older release.\n"

    let private execute target scenario =
        let root = fixture.Value

        File.WriteAllText(
            Path.Combine(root, "CHANGELOG.md"),
            changelog scenario.Version scenario.HasChanges
        )

        let directory = Path.Combine(root, "cases", Guid.NewGuid().ToString("N"))

        Directory.CreateDirectory directory
        |> ignore

        checkedRun directory "git" [
            "init"
            "--initial-branch=release-policy-wrong-branch"
        ]
        |> ignore

        let hooks = Path.Combine(directory, "empty-hooks")

        Directory.CreateDirectory hooks
        |> ignore

        checkedRun directory "git" [
            "config"
            "core.hooksPath"
            hooks
        ]
        |> ignore

        checkedRun directory "git" [
            "-c"
            "user.name=Release Policy Test"
            "-c"
            "user.email=release-policy@example.invalid"
            "-c"
            "commit.gpgSign=false"
            "commit"
            "--allow-empty"
            "-m"
            "Release policy fixture"
        ]
        |> ignore

        Expect.equal
            (checkedRun directory "git" [ "remote" ])
            ""
            "The fixture must have no remotes."

        if scenario.Detached then
            let fixtureTag =
                match scenario.Expected, scenario.Tag with
                | Intercepted, Some tag -> tag
                | _ -> "release-policy-fixture"

            checkedRun directory "git" [
                "tag"
                fixtureTag
            ]
            |> ignore

            checkedRun directory "git" [
                "checkout"
                "--detach"
                fixtureTag
            ]
            |> ignore

            let symbolicRef =
                run directory "git" [
                    "symbolic-ref"
                    "--quiet"
                    "HEAD"
                ] []

            Expect.equal
                symbolicRef.ExitCode
                1
                $"Expected a detached checkout.\n{symbolicRef.Output}"

            Expect.equal
                (checkedRun directory "git" [
                    "rev-parse"
                    "HEAD"
                ])
                (checkedRun directory "git" [
                    "rev-parse"
                    "refs/tags/"
                    + fixtureTag
                ])
                "HEAD must point at the synthetic tag."

        let assemblyDirectory = Path.Combine(root, "build/bin/Debug/net8.0")

        let reference name =
            $"#r @\"{Path.Combine(assemblyDirectory, name)}\""

        let invocation =
            match target with
            | "LocalRelease" -> "Build.allReleaseChecks ()"
            | _ ->
                $"Build.initTargets ()\n    let result = Target.runSimple \"{target}\" []\n    result.Error |> Option.iter raise"

        let script =
            String.concat "\n" [
                reference "Fake.Core.Context.dll"
                reference "Fake.Core.Target.dll"
                reference "build.dll"
                "open Fake.Core"
                "Context.FakeExecutionContext.Create false \"build.fsx\" [] |> Context.RuntimeContext.Fake |> Context.setExecutionContext"
                "try"
                "    "
                + invocation
                "    System.IO.File.WriteAllText(\"outcome.txt\", \"returned\")"
                "with"
                "| Build.PublicationIntercepted _ -> System.IO.File.WriteAllText(\"outcome.txt\", \"intercepted\")"
                "| ex -> System.IO.File.WriteAllText(\"outcome.txt\", \"rejected: \" + ex.Message)"
            ]

        File.WriteAllText(Path.Combine(directory, "probe.fsx"), script)

        let environment = [
            "GITHUB_TOKEN", "release-policy-dummy-token"
            match scenario.CI with
            | Some ci -> "CI", ci
            | None -> ()
            match scenario.Tag with
            | Some tag -> "RELEASE_TAG", tag
            | None -> ()
        ]

        let result =
            run
                directory
                "dotnet"
                [
                    "fsi"
                    "--exec"
                    "probe.fsx"
                ]
                environment

        let diagnostic = $"{scenario.Name}, {target}, fixture {directory}\n{result.Output}"
        Expect.equal result.ExitCode 0 diagnostic
        let outcome = File.ReadAllText(Path.Combine(directory, "outcome.txt"))
        let attemptsPath = Path.Combine(directory, "attempts.txt")

        let attempts =
            if File.Exists attemptsPath then
                File.ReadAllLines attemptsPath
                |> Array.toList
            else
                []

        let expectedOutcome, expectedAttempts =
            match scenario.Expected with
            | Intercepted ->
                "intercepted",
                [
                    if target = "PublishToNuGet" then "NuGet" else "GitHub"
                ]
            | Rejected message ->
                "rejected: "
                + message,
                []

        Expect.equal (outcome, attempts) (expectedOutcome, expectedAttempts) diagnostic

    let private localError = "Not on CI. If you want to publish, please use CI."
    let private tagError = "RELEASE_TAG must equal v8.7.6."

    let private stable = {
        Name = "detached matching stable tag"
        CI = Some "true"
        Tag = Some "v8.7.6"
        Version = "8.7.6"
        HasChanges = true
        Detached = true
        Expected = Intercepted
    }

    let private scenarios = [
        stable
        {
            stable with
                Name = "CI unset"
                CI = None
                Expected = Rejected localError
        }
        {
            stable with
                Name = "CI false"
                CI = Some "false"
                Expected = Rejected localError
        }
        {
            stable with
                Name = "missing tag"
                Tag = None
                Expected = Rejected tagError
        }
        {
            stable with
                Name = "mismatched tag"
                Tag = Some "v8.7.5"
                Expected = Rejected tagError
        }
        {
            stable with
                Name = "detached matching prerelease tag"
                Version = "8.8.0-rc.2"
                Tag = Some "v8.8.0-rc.2"
        }
        {
            stable with
                Name = "latest changelog version"
                Version = "9.2.1"
                Tag = Some "v9.2.1"
        }
        {
            stable with
                Name = "older changelog tag"
                Version = "9.2.1"
                Tag = Some "v7.0.0"
                Expected = Rejected "RELEASE_TAG must equal v9.2.1."
        }
        {
            stable with
                Name = "empty changelog"
                HasChanges = false
                Expected =
                    Rejected
                        "No changes in CHANGELOG. Please add your changes under a heading specified in https://keepachangelog.com/"
        }
        for name, tag in
            [
                "empty tag", ""
                "extra prefix", "vv8.7.6"
                "partial version", "v8.7"
                "leading whitespace", " v8.7.6"
                "trailing whitespace", "v8.7.6 "
                "suffix", "v8.7.6-extra"
                "wrong case", "V8.7.6"
                "ref prefix", "refs/tags/v8.7.6"
            ] do
            {
                stable with
                    Name = name
                    Tag = Some tag
                    Expected = Rejected tagError
            }
    ]

    [<Tests>]
    let tests =
        testList "ReleasePolicy" [
            for target in
                [
                    "PublishToNuGet"
                    "GitHubRelease"
                ] do
                for scenario in scenarios do
                    testCase $"{target} {scenario.Name}" (fun () -> execute target scenario)
            for ci in
                [
                    None
                    Some "false"
                ] do
                testCase
                    $"LocalRelease wrong branch CI {ci}"
                    (fun () ->
                        execute "LocalRelease" {
                            stable with
                                CI = ci
                                Detached = false
                                Expected =
                                    Rejected
                                        "Not on master.  If you want to release please switch to this branch."
                        }
                    )
            testCase
                "workflow passes the exact tag to Publish"
                (fun () ->
                    let workflow =
                        File
                            .ReadAllText(Path.Combine(sourceRoot, ".github/workflows/publish.yml"))
                            .Replace("\r\n", "\n")

                    Expect.stringContains
                        workflow
                        "tags:\n      - \"v*\""
                        "Only v-prefixed tags must trigger publication."

                    Expect.stringContains
                        workflow
                        "      - name: publish\n        env:\n          RELEASE_TAG: ${{ github.ref_name }}\n"
                        "The Publish step must receive the unmodified tag."

                    Expect.stringContains
                        workflow
                        "./build.sh Publish"
                        "The workflow must invoke the guarded Publish graph."
                )
            testCase
                "template Changelog.fs copies match the root build"
                (fun () ->
                    let read relativePath =
                        File.ReadAllBytes(Path.Combine(sourceRoot, relativePath))

                    let root = read "build/Changelog.fs"

                    for copy in
                        [
                            "Content/Library/build/Changelog.fs"
                            "Content/Console/build/Changelog.fs"
                        ] do
                        Expect.isTrue
                            (read copy = root)
                            $"{copy} must be byte-identical to build/Changelog.fs"
                )
        ]
        |> testSequenced
