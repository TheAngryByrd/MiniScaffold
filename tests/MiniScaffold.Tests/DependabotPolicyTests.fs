namespace MiniScaffold.Tests

module DependabotPolicyTests =
    open System
    open System.Diagnostics
    open System.IO
    open System.Text.RegularExpressions
    open Expecto

    let private sourceRoot =
        Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "../.."))

    let private trackedFiles =
        lazy
            (let startInfo = ProcessStartInfo("git", "ls-files -z")
             startInfo.WorkingDirectory <- sourceRoot
             startInfo.RedirectStandardOutput <- true
             startInfo.UseShellExecute <- false
             use git = Process.Start startInfo
             let output = git.StandardOutput.ReadToEndAsync()

             if not (git.WaitForExit(TimeSpan.FromMinutes 1.)) then
                 git.Kill true
                 failtest "git ls-files did not exit in one minute"

             Expect.equal git.ExitCode 0 "git ls-files should list the tracked files"

             output.Result.Split('\000', StringSplitOptions.RemoveEmptyEntries)
             |> Array.toList)

    let private config =
        lazy (DependabotConfig.read (Path.Combine(sourceRoot, ".github", "dependabot.yml")))

    let private update ecosystem =
        config.Value.Updates
        |> List.filter (fun update -> update.Ecosystem = ecosystem)
        |> function
            | [ update ] -> update
            | updates ->
                failtestf
                    ".github/dependabot.yml should have one %s block, not %d"
                    ecosystem
                    updates.Length

    let private entryPattern (entry: string) =
        let path = entry.Trim('/')

        if path = "" then
            ""
        else
            Regex.Escape(path).Replace(@"\*", "[^/]+")

    let private directoryOf (file: string) =
        match file.LastIndexOf '/' with
        | -1 -> ""
        | index -> file.Substring(0, index)

    let private isYaml (file: string) =
        file.EndsWith ".yml"
        || file.EndsWith ".yaml"

    // dependabot-core github_actions file_fetcher.rb: "/" reads .github/workflows and a root action.yml or action.yaml, any other entry reads the YAML files in that folder.
    let private actionManifests () =
        trackedFiles.Value
        |> List.filter (fun file ->
            Regex.IsMatch(file, @"(^|/)\.github/workflows/[^/]+\.ya?ml$")
            || Regex.IsMatch(file, @"(^|/)action\.ya?ml$")
        )

    let private actionEntryReads entry (file: string) =
        if entry = "/" then
            Regex.IsMatch(file, @"^\.github/workflows/[^/]+\.ya?ml$")
            || Regex.IsMatch(file, @"^action\.ya?ml$")
        else
            isYaml file
            && Regex.IsMatch(directoryOf file, $"^{entryPattern entry}$")

    // dependabot-core devcontainers file_fetcher.rb: each entry reads .devcontainer.json and .devcontainer/devcontainer.json in that folder.
    // Its .devcontainer/<dir>/devcontainer.json lookup builds a wrong path outside the root.
    let private devcontainerManifests () =
        trackedFiles.Value
        |> List.filter (fun file ->
            Regex.IsMatch(file, @"(^|/)\.devcontainer\.json$")
            || Regex.IsMatch(file, @"(^|/)\.devcontainer/([^/]+/)?devcontainer\.json$")
        )

    let private devcontainerEntryReads entry (file: string) =
        let owner = entryPattern entry
        let prefix = if owner = "" then "" else $"{owner}/"

        Regex.IsMatch(file, $"^{prefix}\\.devcontainer\\.json$")
        || Regex.IsMatch(file, $"^{prefix}\\.devcontainer/devcontainer\\.json$")
        || (owner = ""
            && Regex.IsMatch(file, @"^\.devcontainer/[^/]+/devcontainer\.json$"))

    let private nugetManifests () =
        trackedFiles.Value
        |> List.filter (fun file -> Regex.IsMatch(file, @"(^|/)Directory\.Packages\.props$"))

    let private nugetEntryReads entry (file: string) =
        Regex.IsMatch(directoryOf file, $"^{entryPattern entry}$")

    // dependabot-core NuGet DiscoveryWorker starts only from solution and project files at the top of an entry.
    // Its .config/dotnet-tools.json lookup also searches parent folders, so a missing template manifest reads the root one.
    let private nugetEntryGaps entry =
        let owner = entryPattern entry
        let prefix = if owner = "" then "" else $"{owner}/"

        let has pattern =
            trackedFiles.Value
            |> List.exists (fun file -> Regex.IsMatch(file, $"^{prefix}{pattern}$"))

        [
            if not (has @"[^/]+\.(sln|slnx|csproj|fsproj)") then
                $"{entry} has no solution or project file"
            if not (has @"\.config/dotnet-tools\.json") then
                $"{entry} has no .config/dotnet-tools.json"
        ]

    let private checkCoverage
        (ecosystem: string)
        (manifests: unit -> string list)
        (reads: string -> string -> bool)
        =
        let entries = (update ecosystem).Directories
        Expect.isNonEmpty entries $".github/dependabot.yml should list directories for {ecosystem}"

        let unread =
            manifests ()
            |> List.filter (fun file ->
                not (
                    entries
                    |> List.exists (fun entry -> reads entry file)
                )
            )

        Expect.isEmpty unread $"Dependabot {ecosystem} should read each manifest: %A{unread}"

        let unused =
            entries
            |> List.filter (fun entry ->
                not (
                    manifests ()
                    |> List.exists (reads entry)
                )
            )

        Expect.isEmpty unused $"Each {ecosystem} directory should hold a manifest: %A{unused}"

    [<Tests>]
    let tests =
        testList "DependabotPolicy" [
            testCase "github-actions directories read every workflow and composite action"
            <| fun () -> checkCoverage "github-actions" actionManifests actionEntryReads
            testCase "devcontainers directories read every devcontainer configuration"
            <| fun () -> checkCoverage "devcontainers" devcontainerManifests devcontainerEntryReads
            testCase "nuget directories read every central package file"
            <| fun () -> checkCoverage "nuget" nugetManifests nugetEntryReads
            testCase "nuget directories hold an entry point and a tool manifest"
            <| fun () ->
                let gaps =
                    (update "nuget").Directories
                    |> List.collect nugetEntryGaps

                Expect.isEmpty
                    gaps
                    $"Dependabot nuget directories should discover their packages and tools: %A{gaps}"
            // The generated library keeps a low FSharp.Core dependency (#353), and an ignore applies to every directory of a block.
            testCase "nuget ignores every FSharp.Core update"
            <| fun () ->
                Expect.equal
                    (update "nuget").Ignore
                    [
                        {
                            DependencyName = "FSharp.Core"
                            Keys = set [ "dependency-name" ]
                        }
                    ]
                    ".github/dependabot.yml nuget should ignore all FSharp.Core versions and nothing else"
            testCase "uses version 2"
            <| fun () ->
                Expect.equal config.Value.Version "2" ".github/dependabot.yml should use version 2"
            testCase "each ecosystem updates weekly with one pull request per dependency"
            <| fun () ->
                for update in config.Value.Updates do
                    Expect.equal update.Interval "weekly" $"{update.Ecosystem} should update weekly"

                    Expect.equal
                        (update.Groups
                         |> List.map (fun group -> group.Keys, group.GroupBy))
                        [ set [ "group-by" ], Some "dependency-name" ]
                        $"{update.Ecosystem} should have one group by dependency name across its directories"
        ]
