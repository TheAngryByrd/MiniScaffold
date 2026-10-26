param(
    [string]$ResultsDirectory = (Join-Path ([IO.Path]::GetTempPath()) ('partition-controls-' + [guid]::NewGuid()))
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (Test-Path -LiteralPath $ResultsDirectory) { throw 'Control results directory already exists' }
$null = New-Item -ItemType Directory -Path $ResultsDirectory
$ResultsDirectory = (Resolve-Path -LiteralPath $ResultsDirectory).Path
$verifier = Join-Path $PSScriptRoot 'TestPartitions.ps1'
$revision = 'a' * 40
$inventory = @('samples.generated builds.one', 'samples.generated builds.two', 'samples.other.one', 'samples.other.two')

function Save-Json([string]$Path, $Value) {
    ConvertTo-Json -InputObject $Value -Depth 10 | Set-Content -LiteralPath $Path -Encoding utf8
}

function Edit-Manifest([string]$Directory, [scriptblock]$Edit) {
    $path = Join-Path $Directory 'manifest.json'
    $value = Get-Content -Raw -LiteralPath $path | ConvertFrom-Json -AsHashtable
    & $Edit $value
    Save-Json $path $value
}

function Edit-Trx([string]$Directory, [scriptblock]$Edit) {
    $path = Join-Path $Directory 'outer/outer.trx'
    [xml]$value = Get-Content -Raw -LiteralPath $path
    & $Edit $value
    $value.Save($path)
}

function Update-Hashes([string]$Directory) {
    $hashes = @{}
    foreach ($file in Get-ChildItem -LiteralPath $Directory -Recurse -File | Where-Object Name -NE 'manifest.json') {
        $relative = [IO.Path]::GetRelativePath($Directory, $file.FullName).Replace([IO.Path]::DirectorySeparatorChar, [char]'/')
        $hashes[$relative] = (Get-FileHash -LiteralPath $file.FullName).Hash
    }
    Edit-Manifest $Directory { param($m) $m.files = $hashes }
}

function New-Bundle([string]$Directory, [string]$Partition) {
    $null = New-Item -ItemType Directory -Path (Join-Path $Directory 'outer')
    $include = $Partition -ceq 'generated-builds'
    $selected = @($inventory | Where-Object { $_.Contains('samples.generated builds.') -eq $include })
    $manifest = [ordered]@{
        schemaVersion = 1; runId = '350'; runAttempt = '1'; sourceRevision = $revision
        configuration = 'Release'; os = 'windows-latest'; partition = $Partition
        filter = if ($include) { 'FullyQualifiedName~samples.generated builds.' } else { 'FullyQualifiedName!~samples.generated builds.' }
        platform = 'Windows'; architecture = 'X64'; sdkVersion = '10.0.402'
        sourceFingerprint = 'A' * 64; assemblySha256 = 'B' * 64; adapterSha256 = 'C' * 64
    }
    foreach ($phase in 'sdk', 'metadata', 'discovery', 'execution') {
        $manifest[$phase] = @{
            exitCode = 0; drain = 'complete'; arguments = @('synthetic', $phase)
            startedUtc = '2026-01-01T00:00:00Z'; endedUtc = '2026-01-01T00:00:01Z'
        }
        [IO.File]::WriteAllText((Join-Path $Directory "$phase.stdout.txt"), 'synthetic public fixture')
        [IO.File]::WriteAllText((Join-Path $Directory "$phase.stderr.txt"), '')
    }
    $discovery = "Test run for fixture.dll (.NETCoreApp,Version=v8.0)`nThe following Tests are available:`n" +
        (($inventory | ForEach-Object { '    ' + $_ }) -join "`n") + "`n"
    [IO.File]::WriteAllText((Join-Path $Directory 'discovery.stdout.txt'), $discovery)
    Save-Json (Join-Path $Directory 'manifest.json') $manifest
    Save-Json (Join-Path $Directory 'inventory.json') $inventory
    Save-Json (Join-Path $Directory 'selected.json') $selected
    $results = @()
    $definitions = @()
    foreach ($name in $selected) {
        $id = [guid]::NewGuid().ToString()
        $execution = [guid]::NewGuid().ToString()
        $results += "<UnitTestResult testName='$name' testId='$id' executionId='$execution' outcome='Passed' />"
        $definitions += "<UnitTest name='$name' id='$id' />"
    }
    $trx = "<TestRun xmlns='http://microsoft.com/schemas/VisualStudio/TeamTest/2010'><Results>$($results -join '')</Results>" +
        "<TestDefinitions>$($definitions -join '')</TestDefinitions><ResultSummary outcome='Completed'>" +
        "<Counters total='$($selected.Count)' executed='$($selected.Count)' passed='$($selected.Count)' failed='0' notExecuted='0' /></ResultSummary></TestRun>"
    [IO.File]::WriteAllText((Join-Path $Directory 'outer/outer.trx'), $trx)
    Update-Hashes $Directory
}

$baseline = Join-Path $ResultsDirectory 'fixtures'
foreach ($partition in 'generated-builds', 'other') { New-Bundle (Join-Path $baseline $partition) $partition }
$cases = @(
    @{ Name = 'valid'; Expected = 'Verified 4 tests'; Edit = {} },
    @{ Name = 'valid-reordered'; Expected = 'Verified 4 tests'; Edit = {
        param($d)
        Save-Json (Join-Path $d 'inventory.json') @($inventory[3..0])
        Save-Json (Join-Path $d 'selected.json') @($inventory[1], $inventory[0])
        Edit-Trx $d { param($x) $null = $x.TestRun.Results.AppendChild($x.TestRun.Results.FirstChild) }
    } },
    @{ Name = 'missing-result'; Expected = 'TRX identities differ'; Edit = {
        param($d) Edit-Trx $d { param($x) $null = $x.TestRun.Results.RemoveChild($x.TestRun.Results.FirstChild) }
    } },
    @{ Name = 'duplicate-result'; Expected = 'Duplicate identity in TRX results'; Edit = {
        param($d) Edit-Trx $d { param($x) $null = $x.TestRun.Results.AppendChild($x.TestRun.Results.FirstChild.CloneNode($true)) }
    } },
    @{ Name = 'skipped-result'; Expected = 'Non-passing result:'; Edit = {
        param($d) Edit-Trx $d { param($x) $x.TestRun.Results.FirstChild.outcome = 'NotExecuted' }
    } },
    @{ Name = 'unknown-result'; Expected = 'TRX identities differ'; Edit = {
        param($d) Edit-Trx $d { param($x) $x.TestRun.Results.FirstChild.testName = 'unknown.case' }
    } },
    @{ Name = 'unselected-result'; Expected = 'TRX identities differ'; Edit = {
        param($d) Edit-Trx $d { param($x) $x.TestRun.Results.FirstChild.testName = 'samples.other.one' }
    } },
    @{ Name = 'unknown-result-element'; Expected = 'Unsupported outer TRX result'; Edit = {
        param($d) Edit-Trx $d { param($x) $null = $x.TestRun.Results.AppendChild($x.CreateElement('UnknownResult', $x.DocumentElement.NamespaceURI)) }
    } },
    @{ Name = 'malformed-trx'; Expected = 'Invalid outer TRX XML:'; Edit = {
        param($d) Set-Content -LiteralPath (Join-Path $d 'outer/outer.trx') -Value '<broken'
    } },
    @{ Name = 'wrong-count'; Expected = 'TRX total count disagrees'; Edit = {
        param($d) Edit-Trx $d { param($x) $x.TestRun.ResultSummary.Counters.total = '3' }
    } },
    @{ Name = 'nonzero-counter'; Expected = 'Nonzero TRX counter: failed'; Edit = {
        param($d) Edit-Trx $d { param($x) $x.TestRun.ResultSummary.Counters.failed = '1' }
    } },
    @{ Name = 'failed-summary'; Expected = 'Non-successful TRX summary'; Edit = {
        param($d) Edit-Trx $d { param($x) $x.TestRun.ResultSummary.outcome = 'Failed' }
    } },
    @{ Name = 'wrong-definition'; Expected = 'TRX definition identity mismatch'; Edit = {
        param($d) Edit-Trx $d { param($x) $x.TestRun.Results.FirstChild.testId = [guid]::NewGuid().ToString() }
    } },
    @{ Name = 'duplicate-execution'; Expected = 'Duplicate identity in TRX execution IDs'; Edit = {
        param($d) Edit-Trx $d { param($x) $x.TestRun.Results.LastChild.executionId = $x.TestRun.Results.FirstChild.executionId }
    } },
    @{ Name = 'duplicate-inventory'; Expected = 'Duplicate identity in full inventory'; Edit = {
        param($d) Save-Json (Join-Path $d 'inventory.json') @($inventory + $inventory[0])
    } },
    @{ Name = 'raw-discovery-mismatch'; Expected = 'Raw discovery and inventory disagree'; Edit = {
        param($d) $p = Join-Path $d 'discovery.stdout.txt'
        (Get-Content -Raw -LiteralPath $p).Replace('samples.other.one', 'unknown.case') | Set-Content -LiteralPath $p
    } },
    @{ Name = 'wrong-selection'; Expected = 'Selection does not match the filter'; Edit = {
        param($d) Save-Json (Join-Path $d 'selected.json') @($inventory[0], $inventory[2])
    } },
    @{ Name = 'empty-selection'; Expected = 'Empty or invalid selected inventory'; Edit = {
        param($d) Save-Json (Join-Path $d 'selected.json') @()
    } },
    @{ Name = 'duplicate-selected'; Expected = 'Duplicate identity in selected inventory'; Edit = {
        param($d) Save-Json (Join-Path $d 'selected.json') @($inventory[0], $inventory[0])
    } },
    @{ Name = 'duplicate-raw-discovery'; Expected = 'Duplicate identity in discovery inventory'; Edit = {
        param($d) Add-Content -LiteralPath (Join-Path $d 'discovery.stdout.txt') -Value ('    ' + $inventory[0])
    } },
    @{ Name = 'hash-mismatch'; Expected = 'Artifact hash mismatch:'; SkipHashes = $true; Edit = {
        param($d) Add-Content -LiteralPath (Join-Path $d 'execution.stdout.txt') -Value 'changed'
    } },
    @{ Name = 'wrong-source'; Expected = 'Wrong manifest sourceRevision.'; Edit = {
        param($d) Edit-Manifest $d { param($m) $m.sourceRevision = 'b' * 40 }
    } },
    @{ Name = 'wrong-run'; Expected = 'Wrong manifest runId.'; Edit = {
        param($d) Edit-Manifest $d { param($m) $m.runId = '351' }
    } },
    @{ Name = 'wrong-attempt'; Expected = 'Wrong manifest runAttempt.'; Edit = {
        param($d) Edit-Manifest $d { param($m) $m.runAttempt = '2' }
    } },
    @{ Name = 'wrong-platform'; Expected = 'Worker ran on the wrong platform'; Edit = {
        param($d) Edit-Manifest $d { param($m) $m.platform = 'Linux' }
    } },
    @{ Name = 'assembly-mismatch'; Expected = 'Worker assemblySha256 mismatch'; Edit = {
        param($d) Edit-Manifest $d { param($m) $m.assemblySha256 = 'D' * 64 }
    } },
    @{ Name = 'source-fingerprint-mismatch'; Expected = 'Worker sourceFingerprint mismatch'; Edit = {
        param($d) Edit-Manifest $d { param($m) $m.sourceFingerprint = 'D' * 64 }
    } },
    @{ Name = 'failed-process'; Expected = 'Failed execution process'; Edit = {
        param($d) Edit-Manifest $d { param($m) $m.execution.exitCode = 1 }
    } },
    @{ Name = 'incomplete-drain'; Expected = 'Incomplete execution output drain'; Edit = {
        param($d) Edit-Manifest $d { param($m) $m.execution.drain = 'timeout' }
    } },
    @{ Name = 'missing-artifact'; Expected = 'Missing artifact: outer/outer.trx'; SkipHashes = $true; Edit = {
        param($d) Remove-Item -LiteralPath (Join-Path $d 'outer/outer.trx')
    } },
    @{ Name = 'missing-bundle'; Expected = 'Expected exactly two worker manifests'; SkipHashes = $true; Edit = {
        param($d) Remove-Item -LiteralPath (Join-Path $d 'manifest.json')
    } },
    @{ Name = 'duplicate-bundle'; Expected = 'Missing or duplicate partition bundle.'; Edit = {
        param($d) Edit-Manifest $d { param($m) $m.partition = 'other'; $m.filter = 'FullyQualifiedName!~samples.generated builds.' }
        Save-Json (Join-Path $d 'selected.json') @($inventory[2], $inventory[3])
        Copy-Item -LiteralPath (Join-Path (Split-Path $d -Parent) 'other/outer/outer.trx') -Destination (Join-Path $d 'outer/outer.trx')
    } },
    @{ Name = 'upstream-failure'; Expected = 'Worker matrix did not succeed: failure.'; Workers = 'failure'; Edit = {} },
    @{ Name = 'upstream-skipped'; Expected = 'Worker matrix did not succeed: skipped.'; Workers = 'skipped'; Edit = {} },
    @{ Name = 'upstream-cancelled'; Expected = 'Worker matrix did not succeed: cancelled.'; Workers = 'cancelled'; Edit = {} }
)

$results = @(foreach ($case in $cases) {
    $directory = Join-Path $ResultsDirectory $case.Name
    $bundles = Join-Path $directory 'bundles'
    $null = New-Item -ItemType Directory -Path $bundles
    foreach ($partition in 'generated-builds', 'other') {
        Copy-Item -LiteralPath (Join-Path $baseline $partition) -Destination $bundles -Recurse
    }
    $generated = Join-Path $bundles 'generated-builds'
    & $case.Edit $generated
    if (-not $case.ContainsKey('SkipHashes')) { Update-Hashes $generated }
    $workers = if ($case.ContainsKey('Workers')) { $case.Workers } else { 'success' }
    $arguments = @('-NoProfile', '-NonInteractive', '-File', $verifier, '-Mode', 'Verify',
        '-Configuration', 'Release', '-OperatingSystem', 'windows-latest', '-RunId', '350', '-RunAttempt', '1',
        '-SourceRevision', $revision, '-BundleRoot', $bundles, '-WorkersResult', $workers)
    Save-Json (Join-Path $directory 'command.json') @{ executable = 'pwsh'; arguments = $arguments }
    & pwsh @arguments 1> (Join-Path $directory 'stdout.txt') 2> (Join-Path $directory 'stderr.txt')
    $exitCode = $LASTEXITCODE
    $text = (Get-Content -Raw -LiteralPath (Join-Path $directory 'stdout.txt')) +
        (Get-Content -Raw -LiteralPath (Join-Path $directory 'stderr.txt'))
    $positive = $case.Name.StartsWith('valid')
    $passed = (($exitCode -eq 0) -eq $positive) -and $text.Contains($case.Expected)
    $needsRerunMessage = $case.Name -in @('wrong-source', 'wrong-run', 'wrong-attempt', 'missing-bundle', 'duplicate-bundle',
        'upstream-failure', 'upstream-skipped', 'upstream-cancelled')
    if ($needsRerunMessage) { $passed = $passed -and $text.Contains('Re-run all jobs') }
    @{ name = $case.Name; positive = $positive; expected = $case.Expected; exitCode = $exitCode; passed = $passed }
})
$failed = @($results | Where-Object { -not $_.passed })
Save-Json (Join-Path $ResultsDirectory 'results.json') @{
    total = $results.Count; passed = $results.Count - $failed.Count; failed = $failed.Count; cases = $results
}
if ($failed.Count -gt 0) {
    throw "Verifier controls failed: $($failed.name -join ', '). Expected failures and raw output: $ResultsDirectory"
}
exit 0
