using TarkovHelper.Models;

namespace TarkovHelper.Services;

/// <summary>
/// Map-related localization strings for LocalizationService.
/// Includes: Map Tracker, Quest Drawer, Map Area, Legend, Settings, etc.
/// </summary>
public partial class LocalizationService
{
    #region Map Tracker Page

    public string MapPositionTracker => "맵 위치 트래커";
    public string MapLabel => "맵:";
    public string QuestMarkers => "퀘스트 마커";
    public string Extracts => "탈출구";
    public string ClearTrail => "경로 지우기";
    public string FullScreen => "전체 화면";
    public string ExitFullScreen => "전체 화면 종료";
    public string StartTracking => "추적 시작";
    public string StopTracking => "추적 중지";
    public string StatusWaiting => "대기 중";
    public string StatusTracking => "추적 중";
    public string PositionLabel => "위치:";
    public string LastUpdateLabel => "마지막 업데이트:";
    public string QuestObjectives => "퀘스트 목표";
    public string ProgressOnThisMap => "이 맵 진행률";
    public string FilterIncomplete => "미완료";
    public string FilterCompleted => "완료";
    public string FilterAllTypes => "전체 타입";
    public string FilterVisit => "방문";
    public string FilterMark => "마킹";
    public string FilterPlant => "설치";
    public string FilterExtract => "탈출";
    public string FilterFind => "찾기";
    public string ThisMapOnly => "이 맵만";
    public string GroupByQuest => "그룹화";
    public string ScreenshotFolder => "스크린샷 폴더";
    public string MarkerSettings => "마커 설정";
    public string HideCompletedObjectives => "완료된 목표 숨기기";
    public string QuestStyle => "퀘스트 스타일:";
    public string QuestNameSize => "퀘스트명:";
    public string QuestMarkerSize => "퀘스트 마커:";
    public string PlayerMarkerSize => "플레이어 마커:";
    public string ExtractSettings => "탈출구 설정";
    public string PmcExtracts => "PMC 탈출구";
    public string ScavExtracts => "Scav 탈출구";
    public string ExtractNameSize => "이름 크기:";
    public string MarkerColors => "마커 색상";
    public string ResetColors => "색상 초기화";
    public string NoMapImage => "맵 이미지가 없습니다";
    public string AddMapImageHint => "Assets/Maps/ 폴더에 맵 이미지를 추가하세요";
    public string SetImagePathHint => "또는 설정에서 이미지 경로를 지정하세요";
    public string ResetView => "초기화";
    public string StyleIconOnly => "아이콘만";
    public string StyleGreenCircle => "녹색 원";
    public string StyleIconWithName => "아이콘 + 이름";
    public string StyleCircleWithName => "원 + 이름";
    public string Quest => "퀘스트";
    public string QuestPanelTooltip => "퀘스트 패널 열기/닫기 (Q)";
    public string ShortcutHelp => "단축키 도움말";
    public string DisplayOptions => "표시 옵션";
    public string CloseWithShortcut => "닫기 (Q)";
    public string SearchPlaceholder => "🔍 검색...";
    public string Incomplete => "미완료";
    public string CurrentMap => "현재 맵";
    public string SortByName => "이름";
    public string SortByProgress => "진행률";
    public string SortByCount => "개수";
    public string NoQuestsToDisplay => "표시할 퀘스트 없음";
    public string TryAdjustingFilters => "필터를 조정해 보세요";
    public string MarkAllComplete => "모두 완료";
    public string MarkAllIncomplete => "모두 미완료";
    public string HideFromMap => "맵에서 숨기기";
    public string ShowHideOnMap => "맵에 표시/숨김";
    public string ViewOnMap => "맵에서 보기";
    public string OpenClose => "열기/닫기";
    public string Move => "이동";
    public string Select => "선택";
    public string GoToMap => "맵이동";
    public string ToggleComplete => "완료토글";
    public string Click => "클릭";
    public string RightClick => "우클릭";
    public string Scroll => "스크롤";
    public string Zoom => "줌";
    public string Drag => "드래그";
    public string LoadingMap => "맵 로딩 중...";
    public string ZoomInTooltip => "확대 (Scroll Up)";
    public string ZoomOutTooltip => "축소 (Scroll Down)";
    public string ResetViewTooltip => "뷰 초기화 (R)";
    public string MapLegend => "맵 범례";
    public string Extract => "탈출구";
    public string TransitPoint => "환승 지점";
    public string QuestObjective => "퀘스트 목표";
    public string QuestType => "퀘스트 타입";
    public string Visit => "방문";
    public string Mark => "마킹";
    public string PlantItem => "아이템 설치";
    public string Kill => "처치";
    public string QuestTypeFilter => "퀘스트 타입 필터";
    public string VisitType => "방문 (Visit)";
    public string MarkType => "마킹 (Mark)";
    public string PlantType => "아이템 설치 (Plant)";
    public string ExtractType => "탈출 (Extract)";
    public string FindType => "아이템 찾기 (Find)";
    public string KillType => "처치 (Kill)";
    public string OtherType => "기타 (Other)";
    public string Minimap => "미니맵";

    // Custom Markers
    public string CustomMarkers => "사용자 마커";
    public string AddCustomMarker => "마커 추가";
    public string DeleteCustomMarker => "마커 삭제";
    public string MarkerName => "마커 이름";
    public string EnterMarkerName => "마커 이름을 입력하세요";
    public string CustomMarkerColor => "마커 색상";

    #endregion

    #region Map Page - Settings

    public string SettingsTitle => "⚙ 설정";
    public string SettingsTooltip => "설정 (레이어, 마커 크기, 트래커)";
    public string TabDisplay => "표시";
    public string TabMarker => "마커";
    public string TabTracker => "트래커";
    public string TabShortcuts => "단축키";
    public string Trail => "이동 경로";
    public string ShowMinimap => "미니맵 표시";
    public string MinimapSize => "미니맵 크기";
    public string QuestFilter => "퀘스트 필터";
    public string MarkerSize => "마커 크기";
    public string MarkerOpacity => "마커 투명도";
    public string QuestDisplay => "퀘스트 표시";
    public string AutoHideCompleted => "완료 퀘스트 자동 숨김";
    public string FadeCompleted => "완료 퀘스트 흐리게";
    public string ShowMarkerLabels => "마커 라벨 표시";
    public string TrackerStatus => "트래커 상태";
    public string NoFolderSelected => "폴더 미선택";
    public string SelectScreenshotFolder => "스크린샷 폴더 선택";
    public string OpenFolder => "폴더 열기";
    public string StartStopTracking => "트래킹 시작/중지";
    public string ClearPath => "경로 초기화";
    public string PathSettings => "경로 설정";
    public string PathColor => "경로 색상";
    public string PathThickness => "경로 두께";
    public string AutoTrackOnMapLoad => "맵 로드시 자동 추적";
    public string MapControls => "맵 조작";
    public string ZoomInOut => "확대/축소";
    public string PanMap => "맵 이동";
    public string LayerToggle => "레이어 토글";
    public string ShowHideExtracts => "탈출구 표시/숨김";
    public string ShowHideTransit => "환승 표시/숨김";
    public string ShowHideQuests => "퀘스트 표시/숨김";
    public string Panel => "패널";
    public string QuestPanel => "퀘스트 패널";
    public string FloorChange => "층 변경 (다층맵)";
    public string ResetAllSettings => "모든 설정 초기화";

    #endregion

    #region Map Page - Status Bar

    public string SelectMap => "맵 선택";
    public string CopyCoordinates => "좌표 복사";

    #endregion

    #region MapTrackerPage

    // Sidebar section headers
    public string MapTrackerLayers => "레이어";
    public string MapTrackerPointsOfInterest => "관심 지점";
    public string MapTrackerEnemies => "적";
    public string MapTrackerInteractables => "상호작용";
    public string MapTrackerQuests => "퀘스트";
    public string MapTrackerQuickActions => "빠른 실행";
    public string MapTrackerShortcuts => "단축키";

    // Layer names
    public string MapTrackerExtractions => "탈출구";
    public string MapTrackerTransits => "환승";
    public string MapTrackerSpawns => "스폰";
    public string MapTrackerBosses => "보스";
    public string MapTrackerLevers => "레버";
    public string MapTrackerKeys => "열쇠";
    public string MapTrackerQuestObjectives => "퀘스트 목표";

    // Quick actions
    public string MapTrackerShowAllLayers => "모든 레이어 표시";
    public string MapTrackerHideAllLayers => "모든 레이어 숨기기";

    // Status bar
    public string MapTrackerMarkersLabel => "마커:";
    public string MapTrackerQuestsLabel => "퀘스트:";
    public string MapTrackerCursorLabel => "커서:";
    public string MapTrackerSelectMapMessage => "위 드롭다운에서 맵을 선택하세요";
    public string MapTrackerLoadingMap => "맵 로딩 중...";

    // Top bar
    public string MapTrackerMapLabel => "맵:";
    public string MapTrackerFloorLabel => "층:";
    public string MapTrackerPlayerLabel => "플레이어:";
    public string MapTrackerAutoFloor => "자동";

    public string MapTrackerAll => "전체";
    public string MapTrackerNone => "없음";

    public string MapTrackerStart => "시작";
    public string MapTrackerStop => "중지";

    public string MapTrackerReady => "준비 완료";
    public string MapTrackerKeyboardShortcuts => "키보드 단축키";

    // Marker type localization method
    public string GetMarkerTypeName(MarkerType type) => type switch
    {
        MarkerType.PmcExtraction => "PMC 탈출구",
        MarkerType.ScavExtraction => "스캐브 탈출구",
        MarkerType.SharedExtraction => "공유 탈출구",
        MarkerType.Transit => "환승 지점",
        MarkerType.PmcSpawn => "PMC 스폰",
        MarkerType.ScavSpawn => "스캐브 스폰",
        MarkerType.BossSpawn => "보스 스폰",
        MarkerType.RaiderSpawn => "레이더 스폰",
        MarkerType.Lever => "레버/스위치",
        MarkerType.Keys => "열쇠 위치",
        _ => "알 수 없음"
    };

    #endregion
}
