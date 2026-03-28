$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "..\src\ChashApp\ChashApp.csproj"
$output = Join-Path $PSScriptRoot "..\artifacts\publish\win-x64"

dotnet restore $project
dotnet publish $project `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishReadyToRun=true `
  -p:PublishSingleFile=false `
  -o $output

Write-Host "Published to: $output"
