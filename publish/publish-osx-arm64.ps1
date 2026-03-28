$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "..\src\ChashApp\ChashApp.csproj"
$output = Join-Path $PSScriptRoot "..\artifacts\publish\osx-arm64"

dotnet restore $project
dotnet publish $project `
  -c Release `
  -r osx-arm64 `
  --self-contained true `
  -p:PublishReadyToRun=true `
  -p:PublishSingleFile=false `
  -o $output

Write-Host "Published to: $output"
