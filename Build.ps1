param(
    [switch]$NoBuild,
    [string]$Profile
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.Drawing
$package = Join-Path $PSScriptRoot 'Thunderstore'
$manifest = Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $package 'manifest.json') | ConvertFrom-Json
$name = $manifest.name
if ($name -notmatch '^[A-Za-z0-9_]+$' -or $manifest.description.Length -gt 250 -or
    $manifest.version_number -notmatch '^\d+\.\d+\.\d+$' -or
    $manifest.website_url -ne "https://github.com/LowxRise/$name") {
    throw 'Invalid package manifest.'
}
$iconPath = Join-Path $package 'icon.png'
$icon = [Drawing.Image]::FromFile($iconPath)
try {
    if ($icon.Width -ne 256 -or $icon.Height -ne 256 -or
        $icon.RawFormat.Guid -ne [Drawing.Imaging.ImageFormat]::Png.Guid) {
        throw 'icon.png must be a 256x256 PNG.'
    }
}
finally { $icon.Dispose() }

if (-not $NoBuild) {
    $buildArgs = @('build', (Join-Path $PSScriptRoot "$name.csproj"), '-c', 'Release', '--nologo', '-p:NuGetAudit=false')
    if ($Profile) { $buildArgs += "-p:R2ModmanProfile=$Profile" }
    & dotnet @buildArgs
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $name" }
    $destination = Join-Path $package "plugins\$name"
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    Copy-Item -Force -LiteralPath (Join-Path $PSScriptRoot "bin\Release\netstandard2.1\$name.dll") -Destination $destination
}
Copy-Item -Force -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination (Join-Path $package 'README.md')
Copy-Item -Force -LiteralPath (Join-Path $PSScriptRoot 'CHANGELOG.md') -Destination (Join-Path $package 'CHANGELOG.md')
$files = @('manifest.json', 'README.md', 'CHANGELOG.md', 'icon.png', "plugins/$name/$name.dll")
foreach ($relative in $files) {
    if (-not (Test-Path -LiteralPath (Join-Path $package $relative))) { throw "Missing package file: $relative" }
}
$zipPath = Join-Path $PSScriptRoot "$name-$($manifest.version_number).zip"
$stream = [IO.File]::Open($zipPath, [IO.FileMode]::Create)
$zip = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($relative in $files) {
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, (Join-Path $package $relative), $relative) | Out-Null
    }
}
finally {
    $zip.Dispose()
    $stream.Dispose()
}
Write-Output "Packaged $zipPath"
