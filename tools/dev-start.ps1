#!/usr/bin/env pwsh
# Build and launch the mVasu dev stack, then pop the dashboard / PWA
# open in the default browser. Two modes:
#
#   Aspire mode  — when src/mVasu.AppHost exists. One process tree,
#                  Aspire dashboard at https://localhost:17216.
#   Direct mode  — when AppHost is absent (e.g. on a feature branch
#                  rebased before the Aspire orchestrator landed).
#                  Spawns mVasu.Api and the Angular dev-server in two
#                  child processes and opens the PWA at :4200 once
#                  the API's /api/health responds.
#
# Either way, Ctrl+C stops everything the script started.
#
#   pwsh tools/dev-start.ps1
#   pwsh tools/dev-start.ps1 -NoBuild
#
# Pure-PowerShell 7+. Requires the .NET SDK; Aspire mode also requires
# the matching `aspire` CLI version that the AppHost project pins.

[CmdletBinding()]
param(
    # Skip the dotnet build step. Useful when iterating quickly and the
    # last build is known good.
    [switch] $NoBuild
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$apphost  = Join-Path $repoRoot 'src/mVasu.AppHost'
$api      = Join-Path $repoRoot 'src/mVasu.Api'
$pwa      = Join-Path $repoRoot 'src/mVasu.Pwa'
$pwaCerts = Join-Path $pwa '.certs'
$pwaCert  = Join-Path $pwaCerts 'localhost.pem'
$pwaKey   = Join-Path $pwaCerts 'localhost.key'

function Ensure-PwaCert {
    # Make sure ng serve has a Chrome-trusted cert pair to load. Angular's
    # auto-generated SSL cert is self-signed and triggers
    # NET::ERR_CERT_AUTHORITY_INVALID; the dotnet dev-cert is already in
    # the Windows trust store, so exporting it once gets us a clean
    # https://localhost:4200 with no browser warnings.
    if ((Test-Path $pwaCert) -and (Test-Path $pwaKey)) { return }

    Write-Host "Exporting dotnet dev-cert for Angular ($pwaCerts)..." -ForegroundColor Cyan
    if (-not (Test-Path $pwaCerts)) {
        New-Item -ItemType Directory -Path $pwaCerts -Force | Out-Null
    }
    & dotnet dev-certs https --export-path $pwaCert --format Pem --no-password
    if ($LASTEXITCODE -ne 0) {
        Write-Error "dotnet dev-certs export failed (exit code $LASTEXITCODE). Run 'dotnet dev-certs https --trust' first."
        exit 1
    }
}

function Start-AspireMode {
    if (-not $NoBuild) {
        Write-Host "[1/3] Building AppHost..." -ForegroundColor Cyan
        & dotnet build $apphost --nologo --verbosity minimal
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Build failed (exit code $LASTEXITCODE)"
            exit 1
        }
    }

    Write-Host "[2/3] Starting AppHost (Ctrl+C to stop)..." -ForegroundColor Cyan
    Write-Host ""

    # Spawn `dotnet run` as a child process and stream its stdout line-by-
    # line so we can both echo it to this console AND watch for the login
    # URL. Process.Kill($true) takes the whole subtree with it (DCP, the
    # API host, ng serve), which a Stop-Job alone would leave orphaned.
    $psi = [System.Diagnostics.ProcessStartInfo]::new()
    $psi.FileName = 'dotnet'
    $psi.ArgumentList.Add('run')
    $psi.ArgumentList.Add('--project')
    $psi.ArgumentList.Add($apphost)
    $psi.ArgumentList.Add('--launch-profile')
    $psi.ArgumentList.Add('https')
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError  = $true
    $psi.UseShellExecute = $false
    $psi.WorkingDirectory = $repoRoot

    $proc = [System.Diagnostics.Process]::Start($psi)
    $browserOpened = $false

    $stderrJob = Start-ThreadJob -ScriptBlock {
        param($p)
        while (-not $p.StandardError.EndOfStream) {
            $line = $p.StandardError.ReadLine()
            if ($null -ne $line) { [Console]::Error.WriteLine($line) }
        }
    } -ArgumentList $proc

    try {
        while (-not $proc.HasExited) {
            $line = $proc.StandardOutput.ReadLine()
            if ($null -eq $line) { break }
            Write-Host $line
            if (-not $browserOpened -and $line -match 'Login to the dashboard at (https?://\S+)') {
                $url = $Matches[1]
                Write-Host ""
                Write-Host "[3/3] Opening dashboard: $url" -ForegroundColor Green
                Write-Host ""
                Start-Process $url
                $browserOpened = $true
            }
        }
    }
    finally {
        if (-not $proc.HasExited) {
            Write-Host ""
            Write-Host "Stopping AppHost..." -ForegroundColor Yellow
            $proc.Kill($true)
            $proc.WaitForExit(5000) | Out-Null
        }
        if ($stderrJob) {
            Stop-Job $stderrJob -ErrorAction SilentlyContinue
            Remove-Job $stderrJob -Force -ErrorAction SilentlyContinue
        }
    }

    exit $proc.ExitCode
}

function Start-DirectMode {
    Write-Host "Aspire AppHost not present on this branch — running API + PWA directly." -ForegroundColor DarkGray
    Write-Host ""

    if (-not $NoBuild) {
        Write-Host "[1/4] Building API..." -ForegroundColor Cyan
        & dotnet build $api --nologo --verbosity minimal
        if ($LASTEXITCODE -ne 0) { Write-Error "Build failed"; exit 1 }
    }

    Write-Host "[2/4] Starting API (https://localhost:7216)..." -ForegroundColor Cyan
    $apiProc = Start-Process -FilePath 'dotnet' `
        -ArgumentList @('run', '--project', $api, '--no-build') `
        -WorkingDirectory $repoRoot `
        -PassThru -NoNewWindow

    Write-Host "[3/4] Starting PWA dev-server (https://localhost:4200)..." -ForegroundColor Cyan
    $pwaProc = Start-Process -FilePath 'npm' `
        -ArgumentList @('start') `
        -WorkingDirectory $pwa `
        -PassThru -NoNewWindow

    # Wait for the API to come alive, then open the PWA in the browser.
    # /api/health is anonymous and synchronous so it answers as soon as
    # Kestrel is listening — no need to drag XPO into the readiness probe.
    $deadline = (Get-Date).AddSeconds(60)
    $opened = $false
    while ((Get-Date) -lt $deadline -and -not $opened) {
        try {
            $r = Invoke-WebRequest -Uri 'https://localhost:7216/api/health' `
                -SkipCertificateCheck -TimeoutSec 1 -ErrorAction Stop
            if ($r.StatusCode -eq 200) {
                Write-Host "[4/4] Opening PWA: https://localhost:4200" -ForegroundColor Green
                Start-Process 'https://localhost:4200'
                $opened = $true
            }
        } catch {
            Start-Sleep -Milliseconds 500
        }
    }
    if (-not $opened) {
        Write-Warning "API not ready within 60s — open https://localhost:4200 manually once both processes are up."
    }

    Write-Host ""
    Write-Host "API + PWA running (Ctrl+C to stop both)" -ForegroundColor Cyan

    try {
        while (-not $apiProc.HasExited -and -not $pwaProc.HasExited) {
            Start-Sleep -Seconds 1
        }
    }
    finally {
        foreach ($p in @($apiProc, $pwaProc)) {
            if ($p -and -not $p.HasExited) {
                try { $p.Kill($true) } catch { }
                $p.WaitForExit(5000) | Out-Null
            }
        }
    }
}

Ensure-PwaCert

if (Test-Path $apphost) {
    Start-AspireMode
} else {
    Start-DirectMode
}
