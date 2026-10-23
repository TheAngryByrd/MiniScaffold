namespace MiniScaffold.Tests

module TrustedPublishingTests =
    open System
    open System.IO
    open System.IO.Compression
    open System.Text.RegularExpressions
    open Expecto
    open Infrastructure

    type private WorkflowCheck = {
        Name: string
        Accepts: string -> bool
    }

    type private WorkflowMutation = {
        Name: string
        Change: string -> string
        RejectedBy: string
    }

    type private LibraryFiles = { Workflow: string; Readme: string }

    let private repository =
        Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "../.."))

    let private templateSetup =
        lazy
            (Tests.tests
             |> ignore)

    let private lines (text: string) =
        text.Replace("\r\n", "\n").Split('\n')
        |> Array.toList

    let private block (header: string) (source: string list) =
        match
            source
            |> List.tryFindIndex ((=) header)
        with
        | None -> []
        | Some index ->
            let indent =
                header.Length
                - header.TrimStart().Length

            source
            |> List.skip (index + 1)
            |> List.takeWhile (fun line ->
                String.IsNullOrWhiteSpace line
                || line.Length
                   - line.TrimStart().Length > indent
            )

    let private buildBlock text =
        text
        |> lines
        |> block "jobs:"
        |> block "  build:"

    let private steps text =
        text
        |> buildBlock
        |> block "    steps:"
        |> List.fold
            (fun groups line ->
                match groups with
                | _ when line.StartsWith("      - ", StringComparison.Ordinal) ->
                    [ line ]
                    :: groups
                | head :: tail ->
                    (line
                     :: head)
                    :: tail
                | [] -> []
            )
            []
        |> List.rev
        |> List.map List.rev

    let private loginStep step =
        List.contains "        uses: NuGet/login@v1" step

    let private publishStep step =
        List.tryHead step = Some "      - name: publish"

    let private publishBlock text =
        steps text
        |> List.tryFind publishStep
        |> Option.defaultValue []

    let private workflowChecks = [
        {
            Name = "publishing job owns both permissions"
            Accepts =
                fun text ->
                    let permissions =
                        buildBlock text
                        |> block "    permissions:"

                    List.contains "      contents: write" permissions
                    && List.contains "      id-token: write" permissions
                    && Regex.Matches(text, @"(?m)^\s*id-token:").Count = 1
        }
        {
            Name = "publishing job uses the nuget environment"
            Accepts =
                fun text ->
                    buildBlock text
                    |> block "    environment:"
                    |> List.contains "      name: nuget"
        }
        {
            Name = "publishing job has one NuGet login"
            Accepts =
                fun text ->
                    (steps text
                     |> List.filter loginStep
                     |> List.length) = 1
                    && Regex.Matches(text, @"(?m)^\s*uses: NuGet/login@").Count = 1
        }
        {
            Name = "login uses the environment profile variable"
            Accepts =
                fun text ->
                    steps text
                    |> List.exists (fun step ->
                        loginStep step
                        && List.contains "        id: nuget_login" step
                        && (block "        with:" step
                            |> List.contains "          user: ${{ vars.NUGET_USER }}")
                    )
        }
        {
            Name = "login immediately precedes publication"
            Accepts =
                fun text ->
                    steps text
                    |> List.pairwise
                    |> List.exists (fun (login, publish) ->
                        loginStep login
                        && publishStep publish
                    )
        }
        {
            Name = "publication consumes only the login output"
            Accepts =
                fun text ->
                    let env =
                        publishBlock text
                        |> block "        env:"

                    List.contains
                        "          NUGET_TOKEN: ${{ steps.nuget_login.outputs.NUGET_API_KEY }}"
                        env
                    && not (text.Contains("secrets.NUGET_TOKEN", StringComparison.Ordinal))
                    && not (text.Contains("secrets.NUGET_USER", StringComparison.Ordinal))
                    && not (text.Contains("||", StringComparison.Ordinal))
        }
        {
            Name = "publication preserves the tag guard and GitHub release inputs"
            Accepts =
                fun text ->
                    let env =
                        publishBlock text
                        |> block "        env:"

                    let tags =
                        lines text
                        |> block "on:"
                        |> block "  push:"
                        |> block "    tags:"

                    List.contains "      - \"v*\"" tags
                    && List.tryHead env = Some "          RELEASE_TAG: ${{ github.ref_name }}"
                    && List.contains "          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}" env
                    && (publishBlock text
                        |> block "        run: |"
                        |> List.contains "          ./build.sh Publish")
        }
    ]

    let private packedLibrary =
        lazy
            (use archive = ZipFile.OpenRead Tests.nugetPkgPath

             let read path =
                 let entry = archive.GetEntry path
                 Expect.isNotNull entry $"The template package must contain {path}"
                 use stream = entry.Open()
                 use reader = new StreamReader(stream)
                 reader.ReadToEnd()

             {
                 Workflow = read "Content/Library/.github/workflows/publish.yml"
                 Readme = read "Content/Library/README.md"
             })

    let private generatedLibrary =
        lazy
            (templateSetup.Force()
             use generated = Disposables.DisposableDirectory.Create()
             Tests.copyGlobalJson generated.DirectoryInfo

             Tests.runTemplate
                 generated.Directory
                 "-n TrustedPublishingFixture --githubUsername ExampleOwner --outputType library --releaseBranch release/testing"

             let directory = Path.Combine(generated.Directory, "TrustedPublishingFixture")

             {
                 Workflow =
                     File.ReadAllText(Path.Combine(directory, ".github/workflows/publish.yml"))
                 Readme = File.ReadAllText(Path.Combine(directory, "README.md"))
             })

    let private hasPolicy owner repositoryName (text: string) =
        [
            $"| GitHub owner | `{owner}` |"
            $"| Repository | `{repositoryName}` |"
            "| Workflow filename | `publish.yml` |"
            "| Environment | `nuget` |"
        ]
        |> List.forall (fun field -> text.Contains(field, StringComparison.Ordinal))

    let private hasProfileSetup (text: string) =
        text.Contains("NUGET_USER", StringComparison.Ordinal)
        && Regex.IsMatch(text, @"(?i)environment variable")
        && Regex.IsMatch(text, @"(?i)NuGet profile")
        && Regex.IsMatch(text, @"(?i)email")

    let private hasRetirementGates (text: string) =
        let retirement =
            Regex.Match(text, @"(?is)Only after.{0,200}remove.{0,100}NUGET_TOKEN.{0,150}revoke")

        retirement.Success
        && [
            "NuGet"
            "GitHub release"
            "credential fragments"
            "active"
            "permanent"
           ]
           |> List.forall (fun requirement ->
               text
                   .Substring(0, retirement.Index)
                   .Contains(requirement, StringComparison.OrdinalIgnoreCase)
           )

    let private hasNoStoredKeySetup (text: string) =
        not (
            Regex.IsMatch(
                text,
                @"(?im)^.*(?:Add (?:your|a secret named) `NUGET_TOKEN`|set your NuGet keys|add our NuGet API key).*$"
            )
        )

    let private mutations = [
        {
            Name = "missing GitHub release permission"
            Change = fun text -> text.Replace("      contents: write\n", "")
            RejectedBy = "publishing job owns both permissions"
        }
        {
            Name = "login moved to another job"
            Change =
                fun text ->
                    let login =
                        "      - name: NuGet login\n        id: nuget_login\n        uses: NuGet/login@v1\n        with:\n          user: ${{ vars.NUGET_USER }}\n"

                    text
                        .Replace(login, "")
                        .Replace(
                            "jobs:\n",
                            "jobs:\n  unrelated:\n    runs-on: ubuntu-latest\n    steps:\n"
                            + login
                        )
            RejectedBy = "publishing job has one NuGet login"
        }
        {
            Name = "missing OIDC permission"
            Change = fun text -> text.Replace("      id-token: write\n", "")
            RejectedBy = "publishing job owns both permissions"
        }
        {
            Name = "workflow-wide OIDC permission"
            Change =
                fun text ->
                    text
                        .Replace("      id-token: write\n", "")
                        .Replace("jobs:\n", "permissions:\n  id-token: write\njobs:\n")
            RejectedBy = "publishing job owns both permissions"
        }
        {
            Name = "permissions moved to another job"
            Change =
                fun text ->
                    text
                        .Replace(
                            "    permissions:\n      contents: write\n      id-token: write\n",
                            ""
                        )
                        .Replace(
                            "jobs:\n",
                            "jobs:\n  unrelated:\n    permissions:\n      contents: write\n      id-token: write\n    runs-on: ubuntu-latest\n    steps:\n      - run: echo unrelated\n"
                        )
            RejectedBy = "publishing job owns both permissions"
        }
        {
            Name = "secret profile input"
            Change = fun text -> text.Replace("vars.NUGET_USER", "secrets.NUGET_USER")
            RejectedBy = "login uses the environment profile variable"
        }
        {
            Name = "wrong output step ID"
            Change = fun text -> text.Replace("steps.nuget_login.outputs", "steps.other.outputs")
            RejectedBy = "publication consumes only the login output"
        }
        {
            Name = "output moved outside publish env"
            Change =
                fun text ->
                    text
                        .Replace(
                            "          NUGET_TOKEN: ${{ steps.nuget_login.outputs.NUGET_API_KEY }}\n",
                            ""
                        )
                        .Replace(
                            "env:\n  CONFIGURATION:",
                            "env:\n  NUGET_TOKEN: ${{ steps.nuget_login.outputs.NUGET_API_KEY }}\n  CONFIGURATION:"
                        )
            RejectedBy = "publication consumes only the login output"
        }
        {
            Name = "intervening step"
            Change =
                fun text ->
                    text.Replace(
                        "      - name: publish",
                        "      - run: echo intervening\n      - name: publish"
                    )
            RejectedBy = "login immediately precedes publication"
        }
        {
            Name = "long-lived secret fallback"
            Change =
                fun text ->
                    text.Replace(
                        "steps.nuget_login.outputs.NUGET_API_KEY }}",
                        "steps.nuget_login.outputs.NUGET_API_KEY || secrets.NUGET_TOKEN }}"
                    )
            RejectedBy = "publication consumes only the login output"
        }
    ]

    [<Tests>]
    let tests =
        testSequenced
        <| testList "TrustedPublishing" [
            for name, read in
                [
                    "root",
                    fun () ->
                        File.ReadAllText(Path.Combine(repository, ".github/workflows/publish.yml"))
                    "packed Library", fun () -> packedLibrary.Value.Workflow
                    "generated Library", fun () -> generatedLibrary.Value.Workflow
                ] do
                for check in workflowChecks do
                    testCase
                        $"{name}: {check.Name}"
                        (fun _ -> Expect.isTrue (check.Accepts(read ())) $"{name}: {check.Name}")
            testCase
                "root setup identifies the maintainer policy"
                (fun _ ->
                    let text = File.ReadAllText(Path.Combine(repository, "README.md"))

                    Expect.isTrue
                        (hasPolicy "TheAngryByrd" "MiniScaffold" text)
                        "The root policy must identify this repository"
                )
            testCase
                "packed Library setup retains template policy parameters"
                (fun _ ->
                    Expect.isTrue
                        (hasPolicy "MyGithubUsername" "MyLib.1" packedLibrary.Value.Readme)
                        "The packed policy must use the template parameters"
                )
            testCase
                "generated Library setup substitutes the repository and branch"
                (fun _ ->
                    let text = generatedLibrary.Value.Readme

                    Expect.isTrue
                        (hasPolicy "ExampleOwner" "TrustedPublishingFixture" text)
                        "The generated policy must name the generated repository"

                    Expect.stringContains
                        text
                        "ExampleOwner/TrustedPublishingFixture/blob/release/testing/.github/workflows/publish.yml"
                        "The workflow link must use the generated release branch"

                    for placeholder in
                        [
                            "MyGithubUsername"
                            "MyLib.1"
                            "MyReleaseBranch"
                        ] do
                        Expect.isFalse
                            (text.Contains placeholder)
                            $"The generated README must replace {placeholder}"
                )
            for name, read in
                [
                    "root README", fun () -> File.ReadAllText(Path.Combine(repository, "README.md"))
                    "generated README", fun () -> generatedLibrary.Value.Readme
                    "Library tutorial",
                    fun () ->
                        File.ReadAllText(
                            Path.Combine(
                                repository,
                                "docsSrc/Tutorials/Getting_Started_With_Libraries.md"
                            )
                        )
                    "release guide",
                    fun () ->
                        File.ReadAllText(
                            Path.Combine(repository, "docsSrc/How_Tos/release-a-nuget-package.md")
                        )
                ] do
                testCase
                    $"{name}: setup uses a profile variable"
                    (fun _ ->
                        Expect.isTrue
                            (hasProfileSetup (read ()))
                            "Setup must identify the environment variable and NuGet profile, not an email"
                    )

                testCase
                    $"{name}: stored keys are not normal setup"
                    (fun _ ->
                        Expect.isTrue
                            (hasNoStoredKeySetup (read ()))
                            "Normal setup must not require a stored NuGet key"
                    )

                testCase
                    $"{name}: verification precedes key retirement"
                    (fun _ ->
                        Expect.isTrue
                            (hasRetirementGates (read ()))
                            "Package, release, log, and policy checks must precede removal and revocation"
                    )
            for mutation in mutations do
                testCase
                    $"rejects {mutation.Name}"
                    (fun _ ->
                        let source =
                            File
                                .ReadAllText(
                                    Path.Combine(repository, ".github/workflows/publish.yml")
                                )
                                .Replace("\r\n", "\n")

                        let check =
                            workflowChecks
                            |> List.find (fun check -> check.Name = mutation.RejectedBy)

                        Expect.isTrue
                            (check.Accepts source)
                            $"The control must satisfy {check.Name}"

                        let changed = mutation.Change source

                        Expect.notEqual
                            changed
                            source
                            $"The fixture must introduce {mutation.Name}"

                        Expect.isFalse
                            (check.Accepts changed)
                            $"The check must reject {mutation.Name}"
                    )
            testCase
                "rejects an unsubstituted policy owner"
                (fun _ ->
                    let source = generatedLibrary.Value.Readme

                    Expect.isTrue
                        (hasPolicy "ExampleOwner" "TrustedPublishingFixture" source)
                        "The generated control must have the expected policy"

                    let changed =
                        source.Replace(
                            "| GitHub owner | `ExampleOwner` |",
                            "| GitHub owner | `MyGithubUsername` |"
                        )

                    Expect.isFalse
                        (hasPolicy "ExampleOwner" "TrustedPublishingFixture" changed)
                        "The check must reject an unsubstituted policy owner"
                )
        ]
