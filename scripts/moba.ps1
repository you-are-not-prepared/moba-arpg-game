# Runs the local backend and game server in Docker, from any terminal or editor.
# Windows: .\moba start        (moba.cmd in the repo root runs this script)
# Mac/Linux: pwsh scripts/moba.ps1 start
#
#   moba start [all|backend|server]   Start them, rebuilding after changes (all = backend, then game server)
#   moba stop  [all|backend|server]   Stop them (data is kept)
#   moba logs  backend|server         Follow a log (Ctrl+C stops following, not the server)
#   moba status                       Show what's running
#
# The backend is expected at ../moba-arpg-backend (both repos cloned into the same folder).
param(
    [Parameter(Position = 0)]
    [ValidateSet('start', 'stop', 'logs', 'status', 'help')]
    [string]$Action = 'help',

    [Parameter(Position = 1)]
    [ValidateSet('all', 'backend', 'server')]
    [string]$Target = 'all'
)

$gameDir = Split-Path -Parent $PSScriptRoot
$backendDir = Join-Path (Split-Path -Parent $gameDir) 'moba-arpg-backend'
$serverBuild = Join-Path $gameDir 'Builds/LinuxServer/MobaServer.x86_64'

function Fail([string]$message) {
    Write-Host $message -ForegroundColor Red
    exit 1
}

function Assert-Docker {
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        Fail 'Docker not found. Install Docker Desktop: https://www.docker.com/products/docker-desktop/'
    }
    # Not "*> $null": in Windows PowerShell, redirecting a native command's stderr can throw.
    $null = docker info --format '{{.ServerVersion}}' 2>$null
    if ($LASTEXITCODE -ne 0) { Fail 'Docker is not running. Start Docker Desktop and try again.' }
}

function Assert-Backend {
    if (-not (Test-Path (Join-Path $backendDir 'docker-compose.yml'))) {
        Fail ("Backend not found at $backendDir`n" +
            'Clone it next to this repo: git clone https://github.com/you-are-not-prepared/moba-arpg-backend.git')
    }
}

function Assert-ServerBuild {
    if (-not (Test-Path $serverBuild)) {
        Fail 'No server build in Builds/LinuxServer. In Unity: MOBA > Build Linux Server.'
    }
}

# Runs "docker compose <args>" in a repo folder and stops the script if it fails.
function Compose([string]$dir, [string[]]$composeArgs) {
    Push-Location $dir
    try { docker compose @composeArgs } finally { Pop-Location }
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

$doBackend = $Target -in 'all', 'backend'
$doServer = $Target -in 'all', 'server'

switch ($Action) {
    'start' {
        Assert-Docker
        # Check everything first, so a missing server build doesn't leave only the backend running.
        if ($doBackend) { Assert-Backend }
        if ($doServer) { Assert-ServerBuild }
        if ($doBackend) {
            Write-Host '== Backend ==' -ForegroundColor Cyan
            Compose $backendDir @('up', '-d', '--build')
        }
        if ($doServer) {
            Write-Host '== Game server ==' -ForegroundColor Cyan
            Compose $gameDir @('up', '-d', '--build')
        }
        Write-Host ''
        if ($doBackend) { Write-Host 'Backend:     http://localhost:8080/health (may take a few seconds to answer)' }
        if ($doServer) { Write-Host 'Game server: UDP localhost:7777. In the game: Find a match > PvP' }
    }
    'stop' {
        Assert-Docker
        if ($doBackend) { Assert-Backend }
        if ($doServer) { Compose $gameDir @('down') }
        if ($doBackend) { Compose $backendDir @('down') }
    }
    'logs' {
        Assert-Docker
        switch ($Target) {
            'backend' { Assert-Backend; Compose $backendDir @('logs', '-f', 'api') }
            'server' { Compose $gameDir @('logs', '-f', 'game-server') }
            default { Fail 'Which log? moba logs backend, or moba logs server' }
        }
    }
    'status' {
        Assert-Docker
        if ($doBackend) {
            Assert-Backend
            Write-Host '== Backend ==' -ForegroundColor Cyan
            Compose $backendDir @('ps')
        }
        if ($doServer) {
            Write-Host '== Game server ==' -ForegroundColor Cyan
            Compose $gameDir @('ps')
        }
    }
    'help' {
        Write-Host @'
moba start [all|backend|server]   Start them, rebuilding after changes (all = backend, then game server)
moba stop  [all|backend|server]   Stop them (data is kept)
moba logs  backend|server         Follow a log (Ctrl+C stops following, not the server)
moba status                       Show what's running
'@
    }
}
