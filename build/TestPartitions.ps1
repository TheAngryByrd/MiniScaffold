param(
    [Parameter(Mandatory)][ValidateSet('Run', 'Verify')][string]$Mode,
    [Parameter(Mandatory)][ValidateSet('Debug', 'Release')][string]$Configuration,
    [Parameter(Mandatory)][string]$OperatingSystem,
    [Parameter(Mandatory)][string]$RunId,
    [Parameter(Mandatory)][string]$RunAttempt,
    [Parameter(Mandatory)][string]$SourceRevision,
    [string]$Project,
    [string]$Partition,
    [Parameter(Mandatory)][string]$BundleRoot,
    [string]$WorkersResult
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Require([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Read-Json([string]$Path) {
    Require (Test-Path -LiteralPath $Path -PathType Leaf) "Missing artifact: $Path"
    return Get-Content -Raw -LiteralPath $Path | ConvertFrom-Json -NoEnumerate
}

function Write-Json([string]$Path, $Value) {
    ConvertTo-Json -InputObject $Value -Depth 10 | Set-Content -LiteralPath $Path -Encoding utf8
}

function Unique-Names($Names, [string]$Label) {
    Require ($Names -is [array] -and $Names.Count -gt 0) "Empty or invalid $Label"
    $set = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($name in $Names) {
        Require ($name -is [string] -and -not [string]::IsNullOrWhiteSpace($name)) "Invalid identity in $Label"
        Require ($set.Add($name)) "Duplicate identity in ${Label}: $name"
    }
    return ,$set
}

function Discover-Names([string]$Text) {
    $lines = @($Text -split '\r?\n' | Where-Object { $_.Length -gt 0 })
    Require ($lines.Count -gt 2 -and $lines[0] -match '^Test run for .+ \(\.NETCoreApp,Version=v[0-9.]+\)$') 'Unexpected discovery preamble'
    Require ($lines[1] -ceq 'The following Tests are available:') 'Unexpected discovery header'
    $names = @(foreach ($line in $lines[2..($lines.Count - 1)]) {
        Require ($line.StartsWith('    ') -and $line.Length -gt 4) "Unexpected discovery line: $line"
        $line.Substring(4)
    })
    $null = Unique-Names $names 'discovery inventory'
    return ,$names
}

function Get-PartitionPlan([string]$OperatingSystem) {
    switch -CaseSensitive ($OperatingSystem) {
        'macOS-latest' {
            return @(
                @{ Token = 'generated-library-a'; Group = 'samples.generated builds.Library.A.'; Include = $true },
                @{ Token = 'generated-library-b'; Group = 'samples.generated builds.Library.B.'; Include = $true },
                @{ Token = 'generated-console'; Group = 'samples.generated builds.Console.'; Include = $true },
                @{ Token = 'sample-contracts'; Group = 'samples.contracts.'; Include = $true },
                @{ Token = 'other'; Group = 'samples.'; Include = $false }
            )
        }
        { $_ -cin @('ubuntu-latest', 'windows-latest') } {
            return @(
                @{ Token = 'generated-builds'; Group = 'samples.generated builds.'; Include = $true },
                @{ Token = 'other'; Group = 'samples.generated builds.'; Include = $false }
            )
        }
        default { throw 'Unknown worker OS' }
    }
}

function Get-Selection([string]$OperatingSystem, [string]$Partition) {
    $matches = @(Get-PartitionPlan $OperatingSystem | Where-Object { $_.Token -ceq $Partition })
    Require ($matches.Count -eq 1) "Unknown partition '$Partition' for $OperatingSystem"
    return $matches[0]
}

function Get-Filter($Selection) {
    $operator = if ($Selection.Include) { '~' } else { '!~' }
    return "FullyQualifiedName$operator$($Selection.Group)"
}

function Select-Names($Names, $Selection) {
    return ,@($Names | Where-Object { $_.Contains($Selection.Group, [StringComparison]::OrdinalIgnoreCase) -eq $Selection.Include })
}

function Check-Bundle([string]$Directory, $Selection) {
    $manifest = Read-Json (Join-Path $Directory 'manifest.json')
    Require ($manifest.schemaVersion -eq 2) 'Unknown manifest schema'
    foreach ($pair in @(
        @('runId', $RunId), @('runAttempt', $RunAttempt), @('sourceRevision', $SourceRevision),
        @('configuration', $Configuration), @('os', $OperatingSystem), @('partition', $Selection.Token)
    )) {
        Require ($manifest.($pair[0]) -ceq $pair[1]) "Wrong manifest $($pair[0]). Re-run all jobs; bundles must come from the same source, run, and attempt."
    }
    foreach ($field in 'assemblySha256', 'adapterSha256', 'sourceFingerprint') {
        Require ($manifest.$field -cmatch '^[0-9A-F]{64}$') "Invalid $field"
    }
    Require (-not [string]::IsNullOrWhiteSpace($manifest.sdkVersion)) 'Missing SDK version'
    $expectedPlatform = @{ 'ubuntu-latest' = 'Linux'; 'windows-latest' = 'Windows'; 'macOS-latest' = 'macOS' }[$OperatingSystem]
    Require ($manifest.platform -ceq $expectedPlatform) 'Worker ran on the wrong platform'
    Require (-not [string]::IsNullOrWhiteSpace($manifest.architecture)) 'Missing worker architecture'
    foreach ($phase in 'sdk', 'metadata', 'discovery', 'execution') {
        Require ($manifest.$phase.exitCode -eq 0) "Failed $phase process"
        Require ($manifest.$phase.drain -ceq 'complete') "Incomplete $phase output drain"
        Require ([datetimeoffset]::Parse($manifest.$phase.endedUtc) -ge [datetimeoffset]::Parse($manifest.$phase.startedUtc)) "Invalid $phase timestamps"
        Require ($manifest.$phase.arguments.Count -gt 0) "Missing $phase command"
    }
    foreach ($file in 'sdk.stdout.txt', 'sdk.stderr.txt', 'metadata.stdout.txt', 'metadata.stderr.txt',
        'discovery.stdout.txt', 'discovery.stderr.txt', 'execution.stdout.txt', 'execution.stderr.txt',
        'inventory.json', 'selected.json', 'outer/outer.trx') {
        $path = Join-Path $Directory $file
        Require (Test-Path -LiteralPath $path -PathType Leaf) "Missing artifact: $file"
        Require ((Get-FileHash -LiteralPath $path).Hash -ceq $manifest.files.$file) "Artifact hash mismatch: $file"
    }
    $inventory = Read-Json (Join-Path $Directory 'inventory.json')
    $inventorySet = Unique-Names $inventory 'full inventory'
    $rawInventory = Discover-Names (Get-Content -Raw -LiteralPath (Join-Path $Directory 'discovery.stdout.txt'))
    Require ($inventorySet.SetEquals([string[]]$rawInventory)) 'Raw discovery and inventory disagree'
    Require ($manifest.filter -ceq (Get-Filter $Selection)) 'Manifest filter differs from the canonical partition filter'
    $selected = Read-Json (Join-Path $Directory 'selected.json')
    $selectedSet = Unique-Names $selected 'selected inventory'
    Require ($selectedSet.SetEquals([string[]](Select-Names $inventory $selection))) 'Selection does not match the filter'

    try { [xml]$trx = Get-Content -Raw -LiteralPath (Join-Path $Directory 'outer/outer.trx') }
    catch { throw "Invalid outer TRX XML: $($_.Exception.Message)" }
    Require ($trx.DocumentElement.LocalName -ceq 'TestRun' -and $trx.DocumentElement.NamespaceURI -ceq 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010') 'Invalid TRX root'
    $results = @($trx.TestRun.Results.UnitTestResult)
    Require (@($trx.TestRun.Results.ChildNodes | Where-Object NodeType -EQ 'Element').Count -eq $results.Count) 'Unsupported outer TRX result'
    $resultNames = @($results | ForEach-Object { [string]$_.testName })
    $resultSet = Unique-Names $resultNames 'TRX results'
    Require ($resultSet.SetEquals($selectedSet)) 'TRX identities differ from selected inventory'
    $definitions = @($trx.TestRun.TestDefinitions.UnitTest)
    $definitionNames = @($definitions | ForEach-Object { [string]$_.name })
    $definitionSet = Unique-Names $definitionNames 'TRX definitions'
    Require ($definitionSet.SetEquals($selectedSet)) 'TRX definitions differ from selected inventory'
    $null = Unique-Names @($definitions | ForEach-Object { [string]$_.id }) 'TRX definition IDs'
    $null = Unique-Names @($results | ForEach-Object { [string]$_.executionId }) 'TRX execution IDs'
    foreach ($result in $results) {
        Require ($result.outcome -ceq 'Passed') "Non-passing result: $($result.testName) = $($result.outcome)"
        $definition = @($definitions | Where-Object { $_.id -ceq $result.testId })
        Require ($definition.Count -eq 1 -and $definition[0].name -ceq $result.testName) 'TRX definition identity mismatch'
    }
    Require ($trx.TestRun.ResultSummary.outcome -cin @('Completed', 'Passed')) 'Non-successful TRX summary'
    $counters = $trx.TestRun.ResultSummary.Counters
    foreach ($field in 'total', 'executed', 'passed') {
        Require ([int]$counters.$field -eq $selected.Count) "TRX $field count disagrees with inventory"
    }
    foreach ($attribute in $counters.Attributes) {
        if ($attribute.Name -cnotin @('total', 'executed', 'passed')) {
            Require ([int]$attribute.Value -eq 0) "Nonzero TRX counter: $($attribute.Name)"
        }
    }
    return @{ Manifest = $manifest; Inventory = $inventorySet; Selected = $selectedSet }
}

Require ($RunId -match '^\d+$' -and $RunAttempt -match '^[1-9]\d*$') 'Invalid run ID or attempt'
Require ($SourceRevision -match '^[0-9a-f]{40}$') 'Invalid source revision'
$plan = @(Get-PartitionPlan $OperatingSystem)

if ($Mode -eq 'Verify') {
    Require ($WorkersResult -ceq 'success') "Worker matrix did not succeed: $WorkersResult. Re-run all jobs."
    Require (Test-Path -LiteralPath $BundleRoot -PathType Container) 'Missing worker bundles. Re-run all jobs.'
    $manifests = @(Get-ChildItem -LiteralPath $BundleRoot -Recurse -Filter manifest.json -File)
    Require ($manifests.Count -eq $plan.Count) "Expected exactly $($plan.Count) worker manifests from this attempt. Re-run all jobs."
    $checked = @{}
    foreach ($file in $manifests) {
        $token = (Read-Json $file.FullName).partition
        Require ($token -cin $plan.Token) "Unexpected partition bundle '$token' for $OperatingSystem. Re-run all jobs."
        Require (-not $checked.ContainsKey($token)) 'Duplicate partition bundle. Re-run all jobs.'
        $checked[$token] = Check-Bundle $file.DirectoryName (Get-Selection $OperatingSystem $token)
    }
    $first = $checked[$plan[0].Token]
    $union = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($selection in $plan) {
        $bundle = $checked[$selection.Token]
        foreach ($field in 'sourceFingerprint', 'assemblySha256', 'adapterSha256', 'sdkVersion', 'architecture') {
            Require ($first.Manifest.$field -ceq $bundle.Manifest.$field) "Worker $field mismatch"
        }
        Require ($first.Inventory.SetEquals($bundle.Inventory)) 'Worker inventories differ'
        Require (-not $union.Overlaps($bundle.Selected)) 'Partitions overlap'
        $union.UnionWith($bundle.Selected)
    }
    Require ($union.SetEquals($first.Inventory)) 'Partitions do not cover the inventory'
    Write-Host "Verified $($union.Count) tests for $OperatingSystem $Configuration in run $RunId attempt $RunAttempt."
    exit 0
}

$selection = Get-Selection $OperatingSystem $Partition
$filter = Get-Filter $selection
Require (Test-Path -LiteralPath $Project -PathType Leaf) 'Missing test project'
Require (-not (Test-Path -LiteralPath $BundleRoot)) 'Evidence directory already exists'
$null = New-Item -ItemType Directory -Path $BundleRoot
$BundleRoot = (Resolve-Path -LiteralPath $BundleRoot).Path
$repository = Split-Path $PSScriptRoot -Parent
$manifest = [ordered]@{
    schemaVersion = 2; runId = $RunId; runAttempt = $RunAttempt; sourceRevision = $SourceRevision
    configuration = $Configuration; os = $OperatingSystem; partition = $Partition; filter = $Filter
    platform = if ($IsWindows) { 'Windows' } elseif ($IsLinux) { 'Linux' } elseif ($IsMacOS) { 'macOS' } else { throw 'Unsupported worker platform' }
    architecture = [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString()
    osDescription = [Runtime.InteropServices.RuntimeInformation]::OSDescription
}

function Initialize-OutputCapture {
    if ('MiniScaffold.PartitionOutputCapture' -as [type]) { return }
    Add-Type -TypeDefinition @'
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MiniScaffold
{
    public sealed class PartitionOutputCapture
    {
        private readonly Process process;
        private readonly FileStream stdoutFile;
        private readonly FileStream stderrFile;
        private readonly Task stdout;
        private readonly Task stderr;
        private readonly Task readers;
        private readonly Task directExit;
        private readonly Task lifetime;
        private readonly string faultPath;
        private readonly object faultLock = new object();

        public int ProcessId { get; }
        public int ExitCode { get; private set; }
        public string DirectExitUtc { get; private set; }
        public Encoding OutputEncoding { get; }
        public Encoding ErrorEncoding { get; }
        public string OutputStatus => stdout.Status.ToString();
        public string ErrorStatus => stderr.Status.ToString();

        public PartitionOutputCapture(ProcessStartInfo start, string outputPath, string errorPath, string faults)
        {
            faultPath = faults;
            stdoutFile = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 4096, FileOptions.Asynchronous);
            try
            {
                stderrFile = new FileStream(errorPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 4096, FileOptions.Asynchronous);
                process = Process.Start(start);
            }
            catch
            {
                stdoutFile.Dispose();
                stderrFile?.Dispose();
                throw;
            }
            ProcessId = process.Id;
            OutputEncoding = process.StandardOutput.CurrentEncoding;
            ErrorEncoding = process.StandardError.CurrentEncoding;
            stdout = CopyAsync(process.StandardOutput.BaseStream, stdoutFile);
            stderr = CopyAsync(process.StandardError.BaseStream, stderrFile);
            ObserveFault(stdout, "stdout");
            ObserveFault(stderr, "stderr");
            readers = Task.WhenAll(stdout, stderr);
            directExit = WaitForDirectExitAsync();
            lifetime = DisposeAfterCompletionAsync();
        }

        private static async Task CopyAsync(Stream input, FileStream output)
        {
            using (input)
            using (output)
            {
                var buffer = new byte[4096];
                int count;
                while ((count = await input.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) != 0)
                {
                    await output.WriteAsync(buffer, 0, count).ConfigureAwait(false);
                    output.Flush(true);
                }
            }
        }

        private void ObserveFault(Task reader, string name)
        {
            reader.ContinueWith(completed => RecordFault(name, completed.Exception),
                CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        private void RecordFault(string name, Exception error)
        {
            try
            {
                lock (faultLock)
                {
                    File.AppendAllText(faultPath, DateTimeOffset.UtcNow.ToString("O") + " " + name + ": " + error + Environment.NewLine);
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private async Task WaitForDirectExitAsync()
        {
            await process.WaitForExitAsync().ConfigureAwait(false);
            ExitCode = process.ExitCode;
            DirectExitUtc = process.ExitTime.ToUniversalTime().ToString("O");
        }

        private async Task DisposeAfterCompletionAsync()
        {
            try { await Task.WhenAll(readers, directExit).ConfigureAwait(false); }
            catch (Exception error) { RecordFault("capture", error); }
            finally
            {
                try { process.Dispose(); }
                catch (Exception error) { RecordFault("dispose", error); }
            }
        }

        public void WaitForExit() => directExit.GetAwaiter().GetResult();

        public string Drain()
        {
            var finished = Task.WhenAny(readers, Task.Delay(TimeSpan.FromSeconds(30))).GetAwaiter().GetResult();
            if (!ReferenceEquals(finished, readers)) return "timeout";
            try { readers.GetAwaiter().GetResult(); return "complete"; }
            catch { return "faulted"; }
        }
    }
}
'@
}

function Invoke-Recorded([string]$Phase, [string[]]$Arguments) {
    $start = [Diagnostics.ProcessStartInfo]::new('dotnet')
    $start.WorkingDirectory = $repository
    $start.UseShellExecute = $false
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.Environment['DOTNET_CLI_UI_LANGUAGE'] = 'en-US'
    $start.Environment['VSLANG'] = '1033'
    $start.Environment['DOTNET_NOLOGO'] = '1'
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    $outputPath = Join-Path $BundleRoot "$Phase.stdout.txt"
    $errorPath = Join-Path $BundleRoot "$Phase.stderr.txt"
    $faultPath = Join-Path $BundleRoot "$Phase.capture-faults.txt"
    $record = [ordered]@{
        executable = 'dotnet'; arguments = $Arguments; workingDirectory = $repository
        startedUtc = [datetimeoffset]::UtcNow.ToString('O'); endedUtc = $null
        exitCode = $null; processId = $null; directExitUtc = $null; drain = 'not-started'
        stdout = $outputPath; stderr = $errorPath; faults = $faultPath
    }
    $manifest[$Phase] = $record
    Write-Json (Join-Path $BundleRoot 'manifest.json') $manifest
    try {
        Initialize-OutputCapture
        $capture = [MiniScaffold.PartitionOutputCapture]::new($start, $outputPath, $errorPath, $faultPath)
        $record.processId = $capture.ProcessId
        $capture.WaitForExit()
        $record.exitCode = $capture.ExitCode
        $record.directExitUtc = $capture.DirectExitUtc
        $record.drain = 'pending'
        Write-Json (Join-Path $BundleRoot 'manifest.json') $manifest
        $record.drain = $capture.Drain()
        $record.stdoutStatus = $capture.OutputStatus
        $record.stderrStatus = $capture.ErrorStatus
        Require ($record.drain -ceq 'complete') "$Phase exited with code $($record.exitCode), but output drain status is $($record.drain) (limit: 30 seconds). stdout=$($record.stdoutStatus); stderr=$($record.stderrStatus). Partial output: $outputPath; $errorPath. Stream faults: $faultPath"
        $output = [IO.File]::ReadAllText($outputPath, $capture.OutputEncoding)
        $errors = [IO.File]::ReadAllText($errorPath, $capture.ErrorEncoding)
        Write-Host $output
        if ($errors) { Write-Host $errors }
        Require ($record.exitCode -eq 0) "$Phase failed with exit code $($record.exitCode)"
        return $output
    }
    catch {
        $record.failure = $_.Exception.ToString()
        throw
    }
    finally {
        $record.endedUtc = [datetimeoffset]::UtcNow.ToString('O')
        Write-Json (Join-Path $BundleRoot 'manifest.json') $manifest
    }
}

function Source-Fingerprint {
    $files = @(& git -C $repository ls-files --cached --others --exclude-standard)
    Require ($LASTEXITCODE -eq 0 -and $files.Count -gt 0) 'Cannot enumerate source files'
    $records = @($files | Sort-Object -CaseSensitive | ForEach-Object {
        "$_ $((Get-FileHash -LiteralPath (Join-Path $repository $_)).Hash)"
    })
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes(($records -join "`n"))))
}

Require ((& git -C $repository rev-parse HEAD) -ceq $SourceRevision) 'Checkout revision mismatch'
$manifest.sourceFingerprint = Source-Fingerprint
$manifest.sdkVersion = (Invoke-Recorded 'sdk' @('--version')).Trim()
$assembly = (Invoke-Recorded 'metadata' @('msbuild', $Project, '-nologo', "-p:Configuration=$Configuration", '-getProperty:TargetPath')).Trim()
Require (Test-Path -LiteralPath $assembly -PathType Leaf) 'Missing built test assembly'
$adapter = Join-Path (Split-Path $assembly -Parent) 'expecto.visualstudio.dotnetcore.testadapter.dll'
Require (Test-Path -LiteralPath $adapter -PathType Leaf) 'Missing Expecto adapter'
$manifest.assemblyPath = $assembly
$manifest.assemblySha256 = (Get-FileHash -LiteralPath $assembly).Hash
$manifest.adapterSha256 = (Get-FileHash -LiteralPath $adapter).Hash
$common = @('test', $Project, '--configuration', $Configuration, '--no-build', '--no-restore')
$discovery = Invoke-Recorded 'discovery' ($common + @('--list-tests'))
$inventory = Discover-Names $discovery
$selected = Select-Names $inventory $selection
$null = Unique-Names $selected 'selected inventory'
Write-Json (Join-Path $BundleRoot 'inventory.json') $inventory
Write-Json (Join-Path $BundleRoot 'selected.json') $selected
$null = Invoke-Recorded 'execution' ($common + @('--filter', $Filter, '--logger', 'console;verbosity=detailed', '--logger', 'trx;LogFileName=outer.trx', '--results-directory', (Join-Path $BundleRoot 'outer'), '--', 'Expecto.fail-on-focused-tests=true'))
Require ((Get-FileHash -LiteralPath $assembly).Hash -ceq $manifest.assemblySha256) 'Assembly changed during execution'
Require ((Get-FileHash -LiteralPath $adapter).Hash -ceq $manifest.adapterSha256) 'Adapter changed during execution'
Require ((Source-Fingerprint) -ceq $manifest.sourceFingerprint) 'Source changed during execution'
$manifest.files = @{}
foreach ($file in Get-ChildItem -LiteralPath $BundleRoot -Recurse -File | Where-Object Name -NE 'manifest.json') {
    $relative = [IO.Path]::GetRelativePath($BundleRoot, $file.FullName).Replace([IO.Path]::DirectorySeparatorChar, [char]'/')
    $manifest.files[$relative] = (Get-FileHash -LiteralPath $file.FullName).Hash
}
Write-Json (Join-Path $BundleRoot 'manifest.json') $manifest
$null = Check-Bundle $BundleRoot $selection
