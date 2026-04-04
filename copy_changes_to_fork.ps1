# ---------------------------------------------------------
# 변경 사항 복사 스크립트 (Fork 동기화용)
# ---------------------------------------------------------

$CurrentDir = Get-Location
$TargetDir = Read-Host "포크(Fork)한 프로젝트의 로컬 폴더 경로를 입력하세요 (예: C:\Work\TarkovHelper)"

if (-not (Test-Path $TargetDir)) {
    Write-Host "오류: 대상 폴더가 존재하지 않습니다." -ForegroundColor Red
    exit
}

# 복사할 파일 라이브러리 (상대 경로 기준)
$FilesToCopy = @(
    "TarkovHelper\Services\ProfileService.cs",
    "TarkovHelper\Services\UserDataDbService.cs",
    "TarkovHelper\Services\SettingsService.cs",
    "TarkovHelper\Services\HideoutProgressService.cs",
    "TarkovHelper\Services\ItemInventoryService.cs",
    "TarkovHelper\Models\ProfileType.cs",
    "TarkovHelper\MainWindow.xaml",
    "TarkovHelper\MainWindow.xaml.cs",
    "TarkovHelper\Pages\HideoutPage.xaml.cs",
    "TarkovHelper\Pages\ItemsPage.xaml.cs",
    "TarkovHelper\Pages\CollectorPage.xaml.cs",
    "TarkovHelper\App.xaml",
    "TarkovHelper\App.xaml.cs",
    ".gitignore"
)

Write-Host "`n파일 복사를 시작합니다..." -ForegroundColor Cyan

foreach ($File in $FilesToCopy) {
    $SourcePath = Join-Path $CurrentDir $File
    $DestPath = Join-Path $TargetDir $File
    
    # 대상 폴더가 없으면 생성
    $DestDir = Split-Path $DestPath -Parent
    if (-not (Test-Path $DestDir)) {
        New-Item -ItemType Directory -Path $DestDir -Force > $null
    }

    if (Test-Path $SourcePath) {
        Copy-Item -Path $SourcePath -Destination $DestPath -Force
        Write-Host "[복사 완료] $File" -ForegroundColor Green
    } else {
        Write-Host "[누락됨] $File" -ForegroundColor Yellow
    }
}

Write-Host "`n---------------------------------------------------------" -ForegroundColor Cyan
Write-Host "모든 변경 사항이 대상 폴더에 반영되었습니다!" -ForegroundColor Green
Write-Host "이제 대상 폴더에서 git commit 및 push를 진행해 주세요." -ForegroundColor White
Write-Host "---------------------------------------------------------`n"
