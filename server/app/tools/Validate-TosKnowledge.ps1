param(
    [string]$AppRoot = (Resolve-Path "$PSScriptRoot\..").Path,
    [string]$Python = "python",
    [switch]$Rebuild,
    [switch]$FailOnError,
    [int]$Limit = 200,
    [ValidateSet("main", "all")]
    [string]$Scope = "main"
)

$ErrorActionPreference = "Stop"

$AppRoot = (Convert-Path -LiteralPath $AppRoot) -replace '^Microsoft\.PowerShell\.Core\\FileSystem::', ''
$server = Join-Path $AppRoot "tools\tos-knowledge-mcp\server.py"
if (-not (Test-Path -LiteralPath $server)) {
    throw "TOS knowledge MCP server not found: $server"
}

$isWslUnc = $AppRoot -match '^\\\\wsl(?:\.localhost)?\\([^\\]+)\\(.+)$'
if ($isWslUnc) {
    $distro = $Matches[1]
    $linuxAppRoot = "/" + ($Matches[2] -replace '\\', '/')
    $linuxServer = "$linuxAppRoot/tools/tos-knowledge-mcp/server.py"

    if ($Rebuild) {
        & wsl.exe -d $distro -- bash -lc "cd '$linuxAppRoot' && python3 '$linuxServer' --app-root '$linuxAppRoot' build"
        if ($LASTEXITCODE -ne 0) {
            exit $LASTEXITCODE
        }
    }

    $failArg = ""
    if ($FailOnError) {
        $failArg = " --fail-on-error"
    }

    & wsl.exe -d $distro -- bash -lc "cd '$linuxAppRoot' && python3 '$linuxServer' --app-root '$linuxAppRoot' validate --limit '$Limit' --scope '$Scope'$failArg"
    exit $LASTEXITCODE
}

if ($Rebuild) {
    & $Python $server --app-root $AppRoot build
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

$args = @($server, "--app-root", $AppRoot, "validate", "--limit", "$Limit", "--scope", "$Scope")
if ($FailOnError) {
    $args += "--fail-on-error"
}

& $Python @args
exit $LASTEXITCODE
