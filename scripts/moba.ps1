# Runs the local backend and game server in Docker, from any terminal or editor.
# Windows: .\moba start        (moba.cmd in the repo root runs this script)
# Mac/Linux: pwsh scripts/moba.ps1 start
#
#   moba start [all|backend|server]   Start them, rebuilding after changes (all = backend, then game server)
#   moba stop  [all|backend|server]   Stop them (data is kept)
#   moba logs  backend|server         Follow a log (Ctrl+C stops following, not the server)
#   moba status                       Show what's running
#   moba release [-Notes "..."]       Publish Builds/WindowsClient as a GitHub release; players' games offer it
#                                     as an update (version = Player Settings > Version; notes default to the PRs)
#
# The backend is expected at ../moba-arpg-backend (both repos cloned into the same folder).
param(
    [Parameter(Position = 0)]
    [ValidateSet('start', 'stop', 'logs', 'status', 'release', 'help')]
    [string]$Action = 'help',

    [Parameter(Position = 1)]
    [ValidateSet('all', 'backend', 'server')]
    [string]$Target = 'all',

    # moba release: the release notes (otherwise GitHub lists the PRs merged since the last release).
    [string]$Notes
)

$gameDir = Split-Path -Parent $PSScriptRoot
$backendDir = Join-Path (Split-Path -Parent $gameDir) 'moba-arpg-backend'
$serverBuild = Join-Path $gameDir 'Builds/LinuxServer/MobaServer.x86_64'
$clientBuild = Join-Path $gameDir 'Builds/WindowsClient'
$releaseZip = Join-Path $gameDir 'Builds/MobaTest-Windows.zip'  # the name the game's updater looks for
$repo = 'you-are-not-prepared/moba-arpg-game'

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
    'release' {
        if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { Fail 'GitHub CLI not found: https://cli.github.com/' }
        $versionFile = Join-Path $clientBuild 'version.txt'
        if (-not (Test-Path (Join-Path $clientBuild 'MobaTest.exe')) -or -not (Test-Path $versionFile)) {
            Fail 'No client build in Builds/WindowsClient. In Unity: set Player Settings > Version, then MOBA > Build Windows Client.'
        }
        $version = (Get-Content $versionFile -Raw).Trim()
        $tag = "v$version"
        $null = gh release view $tag --repo $repo 2>$null
        if ($LASTEXITCODE -eq 0) {
            Fail "Release $tag already exists. Raise Player Settings > Version and build the client again."
        }
        Write-Host "== Zipping $tag ==" -ForegroundColor Cyan
        # Burst's debug info isn't for players.
        $items = Get-ChildItem $clientBuild | Where-Object { $_.Name -notlike '*DoNotShip*' }
        Compress-Archive -Path $items.FullName -DestinationPath $releaseZip -Force
        Write-Host "== Publishing $tag ==" -ForegroundColor Cyan
        $notesArgs = if ($Notes) { @('--notes', $Notes) } else { @('--generate-notes') }
        gh release create $tag $releaseZip --repo $repo --target main --title $tag @notesArgs
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        Write-Host "Published. Games older than $version offer the update on their start screen."
    }
    'help' {
        Write-Host @'
moba start [all|backend|server]   Start them, rebuilding after changes (all = backend, then game server)
moba stop  [all|backend|server]   Stop them (data is kept)
moba logs  backend|server         Follow a log (Ctrl+C stops following, not the server)
moba status                       Show what's running
moba release [-Notes "..."]       Publish Builds/WindowsClient as a GitHub release (players get it as an update)
'@
    }
}
