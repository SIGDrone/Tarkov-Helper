# Tarkov Helper Build & Publish Automation Script
# 이 스크립트는 버전을 올리고, 빌드한 뒤, 압축하여 BuildOutput 폴더로 이동시킵니다.

$projectFile = "TarkovHelper\TarkovHelper.csproj"
$dbVersionFile = "TarkovHelper\Assets\db_version.txt"
$buildOutputDir = "C:\GitWork\Tarkov-Helper\BuildOutput"

# 절대 경로 확보
$projectPath = Join-Path (Get-Location) $projectFile
$dbVersionPath = Join-Path (Get-Location) $dbVersionFile

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "   Tarkov Helper Publish Automation" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# 1. 버전 읽기 및 증가
if (-not (Test-Path $projectPath)) {
    Write-Error "Project file not found: $projectPath"
    exit 1
}

[xml]$csproj = Get-Content $projectPath
$currentVersion = $csproj.Project.PropertyGroup.Version
Write-Host "[1/4] Current Version: $currentVersion"

$versionParts = $currentVersion.Split('.')
if ($versionParts.Length -ne 3) {
    Write-Error "Invalid version format in csproj: $currentVersion"
    exit 1
}

$newPatch = [int]$versionParts[2] + 1
$newVersion = "$($versionParts[0]).$($versionParts[1]).$newPatch"
Write-Host "      New Version Proposed: $newVersion" -ForegroundColor Green

# 2. 파일 업데이트 (csproj, db_version.txt)
Write-Host "[2/4] Updating version in files..."
$csproj.Project.PropertyGroup.Version = $newVersion
if ($csproj.Project.PropertyGroup.AssemblyVersion) { $csproj.Project.PropertyGroup.AssemblyVersion = $newVersion }
if ($csproj.Project.PropertyGroup.FileVersion) { $csproj.Project.PropertyGroup.FileVersion = $newVersion }
$csproj.Save($projectPath)

$newVersion | Set-Content $dbVersionPath
Write-Host "      Updated $projectFile"
Write-Host "      Updated $dbVersionFile"

# 3. Publish 실행
Write-Host "[3/4] Starting dotnet publish..." -ForegroundColor Yellow
$publishOut = "TarkovHelper\bin\Release\net8.0-windows\win-x64\publish"

# 이전 빌드 잔재 삭제
if (Test-Path $publishOut) { 
    Write-Host "      Cleaning previous publish folder..."
    Remove-Item -Recurse -Force $publishOut 
}

# Publish 명령어 실행 (--self-contained true로 런타임 포함)
dotnet publish $projectPath -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

if ($LASTEXITCODE -ne 0) {
    Write-Host "Error: Build failed!" -ForegroundColor Red
    exit $LASTEXITCODE
}

# 4. 압축 및 이동
Write-Host "[4/4] Packaging..." -ForegroundColor Yellow
if (-not (Test-Path $buildOutputDir)) { 
    New-Item -ItemType Directory -Path $buildOutputDir | Out-Null
}

$zipName = "TarkovHelper_v$newVersion.zip"
$zipPath = Join-Path $buildOutputDir $zipName

if (Test-Path $zipPath) { Remove-Item $zipPath }

Write-Host "      Compressing to $zipName..."
# 빌드 결과물 전체를 압축
Compress-Archive -Path "$publishOut\*" -DestinationPath $zipPath

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host " Successfully published to:" -ForegroundColor Green
Write-Host " $zipPath" -ForegroundColor White
Write-Host "==========================================" -ForegroundColor Cyan
