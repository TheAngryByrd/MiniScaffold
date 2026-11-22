namespace MiniScaffold.Tests

module DevcontainerPolicyTests =
    open System.IO
    open Expecto

    let private sourceRoot =
        Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "../.."))

    let private devcontainers = [
        ""
        "Content/Library"
        "Content/Console"
    ]

    let private lines root file =
        File.ReadAllLines(Path.Combine(sourceRoot, root, ".devcontainer", file))
        |> Array.map _.Trim()

    let private installLine =
        "RUN curl -sS https://starship.rs/install.sh | sh -s -- --yes && starship --version"

    [<Tests>]
    let tests =
        testList "DevcontainerPolicy" [
            for root in devcontainers do
                let name = if root = "" then "root" else root

                // The devcontainers-extra starship feature reads the GitHub API without a token (#362)
                testCase
                    $"{name} devcontainer uses the starship install script and no starship feature"
                <| fun () ->
                    let featureLines =
                        lines root "devcontainer.json"
                        |> Array.filter (fun line ->
                            not (line.StartsWith "//")
                            && line.Contains "features/starship"
                        )

                    Expect.isEmpty
                        featureLines
                        $"{name}/.devcontainer/devcontainer.json should not use a starship feature"

                    Expect.contains
                        (lines root "Dockerfile")
                        installLine
                        $"{name}/.devcontainer/Dockerfile should have the active instruction: {installLine}"
        ]
