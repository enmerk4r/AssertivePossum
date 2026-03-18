param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$Version
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

$projects = @(
    "$scriptDir\AssertivePossum.Goo\AssertivePossum.Goo.csproj"
    "$scriptDir\AssertivePossum\AssertivePossum.csproj"
    "$scriptDir\AssertivePossum.CLI\AssertivePossum.CLI.csproj"
)

foreach ($proj in $projects) {
    if (-not (Test-Path $proj)) {
        Write-Error "Not found: $proj"
        exit 1
    }
    $content = Get-Content $proj -Raw
    $content = $content -replace '<Version>[^<]*</Version>', "<Version>$Version</Version>"
    Set-Content $proj $content -NoNewline
    Write-Host "  $(Split-Path -Leaf $proj) -> $Version"
}

Write-Host ""
Write-Host "All projects set to $Version"
