<#
.SYNOPSIS
    Creates a Yak package from the build output.

.DESCRIPTION
    This script is called as a post-build step to create Yak packages for distribution.
    It creates a dist-X.X.X folder, copies the necessary files, generates manifest.yml
    from the template, and runs yak build to create the .yak package.

.PARAMETER Configuration
    The build configuration (e.g., Release)

.PARAMETER Version
    The version of the plugin (e.g., 0.1.0)

.PARAMETER OutputDir
    The build output directory containing the compiled files

.PARAMETER ProjectDir
    The project directory (where the .csproj is located)

.EXAMPLE
    .\Build-YakPackage.ps1 -Configuration "Release" -Version "0.1.0" -OutputDir "bin\Release\net7.0-windows" -ProjectDir "C:\path\to\AssertivePossum.Components"
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$Configuration,

    [Parameter(Mandatory=$true)]
    [string]$Version,

    [Parameter(Mandatory=$true)]
    [string]$OutputDir,

    [Parameter(Mandatory=$true)]
    [string]$ProjectDir
)

# Only run for Release configurations
if ($Configuration -ne "Release") {
    Write-Host "Skipping Yak package creation for non-Release configuration."
    exit 0
}

# Find yak.exe in common Rhino installation paths
$YakExe = $null
$YakSearchPaths = @(
    "C:\Program Files\Rhino 8\System\Yak.exe",
    "C:\Program Files\Rhino 7\System\Yak.exe"
)

# First check if yak is in PATH
$yakInPath = Get-Command "yak" -ErrorAction SilentlyContinue
if ($yakInPath) {
    $YakExe = $yakInPath.Source
} else {
    # Search common installation paths
    foreach ($path in $YakSearchPaths) {
        if (Test-Path $path) {
            $YakExe = $path
            break
        }
    }
}

if (-not $YakExe) {
    Write-Warning "============================================"
    Write-Warning "Yak CLI tool not found!"
    Write-Warning "Please install Yak or add it to your PATH."
    Write-Warning "Yak is typically found in: C:\Program Files\Rhino 8\System\Yak.exe"
    Write-Warning "Files have been copied to dist folder, but .yak package was not created."
    Write-Warning "You can run 'yak build --platform=win' manually from the dist folder."
    Write-Warning "============================================"
    exit 0  # Exit with success so build doesn't fail
}

Write-Host "Using Yak: $YakExe"

# Paths
$YakDir = Join-Path $ProjectDir "..\..\Yak"
$DistDir = Join-Path $YakDir "dist-$Version"
$TemplateFile = Join-Path $YakDir "manifest-template.yml"
$ManifestFile = Join-Path $DistDir "manifest.yml"

Write-Host "============================================"
Write-Host "Building Yak Package"
Write-Host "============================================"
Write-Host "Configuration: $Configuration"
Write-Host "Version: $Version"
Write-Host "Platform: win"
Write-Host "Output Dir: $OutputDir"
Write-Host "Dist Dir: $DistDir"
Write-Host "============================================"

# Clean and (re)create dist folder
if (Test-Path $DistDir) {
    Write-Host "Cleaning existing directory: $DistDir"
    Remove-Item -Path $DistDir -Recurse -Force
}
Write-Host "Creating directory: $DistDir"
New-Item -ItemType Directory -Path $DistDir -Force | Out-Null

# Files to copy
$FilesToCopy = @(
    "AssertivePossum.gha",
    "AssertivePossum.Goo.dll"
)

# Copy files to dist folder
Write-Host "Copying files to dist folder..."
foreach ($file in $FilesToCopy) {
    $sourcePath = Join-Path $OutputDir $file
    if (Test-Path $sourcePath) {
        Write-Host "  Copying: $file"
        Copy-Item $sourcePath $DistDir -Force
    } else {
        Write-Warning "  File not found: $sourcePath"
    }
}

# Copy icon
$IconSource = Join-Path $YakDir "..\assets\logo_64x64.png"
if (Test-Path $IconSource) {
    Write-Host "  Copying: icon.png"
    Copy-Item $IconSource (Join-Path $DistDir "icon.png") -Force
} else {
    Write-Warning "  Icon not found: $IconSource"
}

# Generate manifest.yml from template
Write-Host ""
Write-Host "Generating manifest.yml from template..."
if (Test-Path $TemplateFile) {
    $manifestContent = Get-Content $TemplateFile -Raw
    $manifestContent = $manifestContent -replace '\{\{VERSION\}\}', $Version
    Set-Content -Path $ManifestFile -Value $manifestContent -NoNewline
    Write-Host "  Manifest created: $ManifestFile"
} else {
    Write-Error "Template file not found: $TemplateFile"
    exit 1
}

# Run yak build for both platforms (win and any)
$Platforms = @("win", "any")

Push-Location $DistDir
try {
    foreach ($platform in $Platforms) {
        Write-Host ""
        Write-Host "Building Yak package (platform=$platform)..."
        $yakArgs = @("build", "--platform=$platform")
        Write-Host "  Running: yak $($yakArgs -join ' ')"

        $process = Start-Process -FilePath $YakExe -ArgumentList $yakArgs -Wait -NoNewWindow -PassThru

        if ($process.ExitCode -eq 0) {
            Write-Host "  Done."
        } else {
            Write-Error "Yak build failed for platform=$platform with exit code: $($process.ExitCode)"
            exit $process.ExitCode
        }
    }

    Write-Host ""
    Write-Host "Yak packages created:"
    $yakFiles = Get-ChildItem -Path $DistDir -Filter "*.yak"
    foreach ($yakFile in $yakFiles) {
        Write-Host "  $($yakFile.Name)"
    }

    # Copy the 'any' package to linux/compute/packages
    $LinuxDir = Join-Path $YakDir "..\linux\compute\packages"
    if (Test-Path $LinuxDir) {
        # Remove old .yak packages
        $oldPackages = Get-ChildItem -Path $LinuxDir -Filter "assertive-possum-*.yak" -ErrorAction SilentlyContinue
        foreach ($old in $oldPackages) {
            Write-Host "  Removing old package: $($old.Name)"
            Remove-Item $old.FullName -Force
        }
        $anyPackage = $yakFiles | Where-Object { $_.Name -match "-any\.yak$" } | Select-Object -First 1
        if ($anyPackage) {
            Copy-Item $anyPackage.FullName $LinuxDir -Force
            Write-Host "  Copied $($anyPackage.Name) to linux/compute/packages/"
        }
    }
} finally {
    Pop-Location
}

Write-Host "============================================"
Write-Host "Yak package build complete!"
Write-Host "============================================"
Write-Host ""
