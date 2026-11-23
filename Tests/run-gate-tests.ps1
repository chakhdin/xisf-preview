# Windows gate of the Sandcastle loop: runs the built test suite with a hard timeout that kills the whole
# process tree. The WindowsOnly (UI) tests run behind the hands-off banner; when the PC is locked they are
# skipped and the gate passes on the other tests.
# Usage: pwsh -NoProfile -File Tests/run-gate-tests.ps1 [-TimeoutSeconds 540]   (after a build of the Tests project)
param(
    [int]$TimeoutSeconds = 540
)

$project = Join-Path $PSScriptRoot 'XisfPreview.Tests.csproj'
$deadline = (Get-Date).AddSeconds($TimeoutSeconds)

# Runs dotnet test with the given filter until the deadline; returns its exit code, 124 on timeout.
function Invoke-TestRun([string]$Filter) {
    $remaining = [int]($deadline - (Get-Date)).TotalMilliseconds
    if ($remaining -le 0) { Write-Host 'TIMEOUT: no time left for this run'; return 124 }
    $process = Start-Process -FilePath 'dotnet' -NoNewWindow -PassThru `
        -ArgumentList @('test', "`"$project`"", '--no-build', '--nologo', '--filter', "`"$Filter`"")
    $null = $process.Handle
    if (-not $process.WaitForExit($remaining)) {
        Write-Host "TIMEOUT: test run exceeded $TimeoutSeconds s, killing process tree $($process.Id)"
        taskkill /PID $process.Id /T /F | Out-Null
        return 124
    }
    return $process.ExitCode
}

$code = Invoke-TestRun 'Category!=WindowsOnly'
if ($code -ne 0) { exit $code }

if (Get-Process LogonUI -ErrorAction SilentlyContinue) {
    Write-Host 'UI tests skipped: PC locked'
    exit 0
}

. (Join-Path ([Environment]::GetFolderPath('UserProfile')) '.claude\scripts\hands-off\hands-off.ps1')
$handsOff = HandsOffStart 'xisf-preview UI tests (Sandcastle gate)' 2
try {
    $code = Invoke-TestRun 'Category=WindowsOnly'
} finally {
    HandsOffEnd $handsOff
}
exit $code
