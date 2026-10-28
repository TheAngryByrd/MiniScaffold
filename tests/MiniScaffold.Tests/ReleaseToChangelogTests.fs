namespace MiniScaffold.Tests

module ReleaseToChangelogTests =
    open System
    open System.Diagnostics
    open System.IO
    open System.Text.RegularExpressions
    open System.Xml.Linq
    open Expecto
    open Infrastructure

    let private repository =
        Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "../.."))

    let private script = Path.Combine(repository, "tools", "releaseToChangelog.fsx")

    let private runScript workingDirectory arguments =
        let startInfo =
            ProcessStartInfo(
                "dotnet",
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            )

        [
            "fsi"
            script
        ]
        @ arguments
        |> List.iter startInfo.ArgumentList.Add

        startInfo.Environment.Remove "MSBuildSDKsPath"
        |> ignore

        startInfo.Environment.Remove "MSBuildExtensionsPath"
        |> ignore

        ProcessCapture.run (TimeSpan.FromSeconds 30.) startInfo

    let private describe (result: ProcessCapture.ProcessOutput) =
        $"exit code {result.ExitCode}\nstdout:\n{result.StandardOutput}\nstderr:\n{result.StandardError}"

    let private releaseNotes =
        """### 0.1.1 - 2020-01-15
* MINOR: Tidy the README
* DOCUMENTATION: Explain the badges
* Read the guide: https://example.com/guide
- BUGFIX: Fix a dash bullet

### 0.2.0 - 2020-02-01
* FEATURE: Add search (https://github.com/test-owner/test-repo/pull/2)
* BUGFIX: Fix crash on empty input
* BUGFIX: #42 Keep issue numbers
* MAINTENANCE: Update dependencies
* FEATURE: Add a dark theme

### 0.1.0 - 2020-01-01
* FEATURE: First release
* -1 is now a valid input
"""

    let private expectedEntries = [
        "## [Unreleased]"
        "## [0.2.0] - 2020-02-01"
        "### Added"
        "- Add search (https://github.com/test-owner/test-repo/pull/2)"
        "- Add a dark theme"
        "### Fixed"
        "- Fix crash on empty input"
        "- #42 Keep issue numbers"
        "### Changed"
        "- Update dependencies"
        "## [0.1.1] - 2020-01-15"
        "### Changed"
        "- Tidy the README"
        "- Read the guide: https://example.com/guide"
        "### DOCUMENTATION"
        "- Explain the badges"
        "### Fixed"
        "- Fix a dash bullet"
        "## [0.1.0] - 2020-01-01"
        "### Added"
        "- First release"
        "### Changed"
        "- -1 is now a valid input"
        "[Unreleased]: https://github.com/test-owner/test-repo/compare/v0.2.0...HEAD"
        "[0.2.0]: https://github.com/test-owner/test-repo/compare/v0.1.1...v0.2.0"
        "[0.1.1]: https://github.com/test-owner/test-repo/compare/v0.1.0...v0.1.1"
        "[0.1.0]: https://github.com/test-owner/test-repo/releases/tag/v0.1.0"
    ]

    let private entriesAndReferences (changelog: string) =
        changelog.Replace("\r\n", "\n").Split('\n')
        |> Array.map _.TrimEnd()
        |> Array.filter (
            String.IsNullOrWhiteSpace
            >> not
        )
        |> Array.skipWhile (fun line -> not (line.StartsWith "## ["))
        |> Array.toList

    let private createReleaseNotes directory =
        let repositoryDirectory = Path.Combine(directory, "generated repo")

        Directory.CreateDirectory repositoryDirectory
        |> ignore

        let path = Path.Combine(repositoryDirectory, "RELEASE_NOTES.md")
        File.WriteAllText(path, releaseNotes)
        path

    let private converts _ =
        use temp = Disposables.DisposableDirectory.Create()
        let releaseNotesPath = createReleaseNotes temp.Directory

        let result =
            runScript temp.Directory [
                "test-owner"
                "test-repo"
                releaseNotesPath
            ]

        Expect.equal
            result.ExitCode
            0
            $"The script must convert the release notes. {describe result}"

        let changelogPath =
            Path.Combine(Path.GetDirectoryName releaseNotesPath, "CHANGELOG.md")

        Expect.isTrue
            (File.Exists changelogPath)
            $"The script must write CHANGELOG.md next to the release notes. {describe result}"

        Expect.sequenceEqual
            (entriesAndReferences (File.ReadAllText changelogPath))
            expectedEntries
            "CHANGELOG.md must group each version's notes by category and link v-prefixed tags of the given repository"

    let private rejectsMissingArgument _ =
        use temp = Disposables.DisposableDirectory.Create()
        let releaseNotesPath = createReleaseNotes temp.Directory

        let result =
            runScript temp.Directory [
                "test-owner"
                releaseNotesPath
            ]

        Expect.equal result.ExitCode 1 $"A missing argument must fail. {describe result}"

        Expect.stringContains
            result.StandardError
            $"Usage: dotnet fsi {script} <github-owner> <github-repository> <release-notes-path>"
            $"A missing argument must print the usage. {describe result}"

        Expect.isFalse
            (File.Exists(Path.Combine(Path.GetDirectoryName releaseNotesPath, "CHANGELOG.md")))
            $"A missing argument must not write CHANGELOG.md. {describe result}"

    let private rejectsMissingInput _ =
        use temp = Disposables.DisposableDirectory.Create()
        let missing = Path.Combine(temp.Directory, "RELEASE_NOTES.md")

        let result =
            runScript temp.Directory [
                "test-owner"
                "test-repo"
                missing
            ]

        Expect.equal result.ExitCode 1 $"A missing input file must fail. {describe result}"

        Expect.stringContains
            result.StandardError
            $"Release notes file not found: {missing}"
            $"A missing input file must name the path. {describe result}"

    let private rejectsReleaseNotesWithoutVersions _ =
        use temp = Disposables.DisposableDirectory.Create()
        let releaseNotesPath = Path.Combine(temp.Directory, "RELEASE_NOTES.md")
        File.WriteAllText(releaseNotesPath, "Release notes\n\nNothing yet\n")

        let result =
            runScript temp.Directory [
                "test-owner"
                "test-repo"
                releaseNotesPath
            ]

        Expect.equal
            result.ExitCode
            1
            $"Release notes without versions must fail. {describe result}"

        Expect.stringContains
            result.StandardError
            $"No versions found in {releaseNotesPath}"
            $"Release notes without versions must be reported. {describe result}"

        Expect.isFalse
            (File.Exists(Path.Combine(temp.Directory, "CHANGELOG.md")))
            $"Release notes without versions must not write CHANGELOG.md. {describe result}"

    let private preservesExistingChangelog _ =
        use temp = Disposables.DisposableDirectory.Create()
        let releaseNotesPath = createReleaseNotes temp.Directory

        let changelogPath =
            Path.Combine(Path.GetDirectoryName releaseNotesPath, "CHANGELOG.md")

        File.WriteAllText(changelogPath, "existing")

        let result =
            runScript temp.Directory [
                "test-owner"
                "test-repo"
                releaseNotesPath
            ]

        Expect.equal
            result.ExitCode
            1
            $"An existing CHANGELOG.md must stop the conversion. {describe result}"

        Expect.stringContains
            result.StandardError
            $"{changelogPath} already exists."
            $"An existing CHANGELOG.md must be reported. {describe result}"

        Expect.equal
            (File.ReadAllText changelogPath)
            "existing"
            $"An existing CHANGELOG.md must stay unchanged. {describe result}"

    let private pinsFakeVersion _ =
        let fakeVersion =
            XDocument
                .Load(Path.Combine(repository, "Directory.Packages.props"))
                .Descendants(XName.Get "FakeVersion")
            |> Seq.exactlyOne
            |> _.Value

        let references =
            Regex.Matches(
                File.ReadAllText script,
                "#r \"nuget: (?<package>Fake\\.[^,\"]+), (?<version>[^\"]+)\""
            )
            |> Seq.map (fun m -> m.Groups["package"].Value, m.Groups["version"].Value)
            |> Seq.toList

        Expect.isNonEmpty
            references
            "The script must reference its FAKE packages with #r \"nuget: ...\""

        for package, version in references do
            Expect.equal
                version
                fakeVersion
                $"{package} must use FakeVersion from Directory.Packages.props"

    [<Tests>]
    let tests =
        testList "release notes to changelog" [
            testCase "converts RELEASE_NOTES.md" converts
            testCase "rejects a missing argument" rejectsMissingArgument
            testCase "rejects a missing input file" rejectsMissingInput
            testCase "rejects release notes without versions" rejectsReleaseNotesWithoutVersions
            testCase "preserves an existing CHANGELOG.md" preservesExistingChangelog
            testCase "pins FakeVersion" pinsFakeVersion
        ]
