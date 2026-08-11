$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'Windows\Clockin.Windows\Clockin.Windows.csproj'
$output = Join-Path $root 'dist\windows'
dotnet publish $project --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true --output $output
Write-Host "Built: $output\Clockin.exe"
