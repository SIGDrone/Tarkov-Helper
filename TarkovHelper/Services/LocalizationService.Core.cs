using System.ComponentModel;
using System.IO;
using System.Text.Json;

namespace TarkovHelper.Services;

/// <summary>
/// Supported languages
/// </summary>
public enum AppLanguage
{
    KO
}

/// <summary>
/// Core functionality for LocalizationService - settings persistence and common UI strings.
/// This is a partial class; other parts contain domain-specific strings.
/// </summary>
public partial class LocalizationService : INotifyPropertyChanged
{
    private static LocalizationService? _instance;
    public static LocalizationService Instance => _instance ??= new LocalizationService();

    private readonly UserDataDbService _userDataDb = UserDataDbService.Instance;
    private const string KeyLanguage = "app.language";

    private const AppLanguage _currentLanguage = AppLanguage.KO;

    public LocalizationService()
    {
    }

    public AppLanguage CurrentLanguage => _currentLanguage;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<AppLanguage>? LanguageChanged;

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #region Settings Persistence (Deprecated - Always KO)

    private void SaveSettings() { }
    private void LoadSettings() { }
    private void MigrateFromJsonIfNeeded() { }

    #endregion

    #region Common UI Strings

    public string Welcome => "Tarkov Helper에 오신 것을 환영합니다";
    public string Cancel => "취소";
    public string Apply => "적용";
    public string Close => "닫기";
    public string Settings => "설정";
    public string Browse => "찾아보기";
    public string Open => "열기";
    public string Start => "시작";
    public string Stop => "중지";
    public string Reset => "리셋";
    public string ResetAll => "초기화";
    public string SelectAll => "전체 선택";
    public string DeselectAll => "전체 해제";
    public string ShowAll => "전체 표시";
    public string HideAll => "전체 숨기기";
    public string ExpandAll => "전체 펼치기";
    public string CollapseAll => "전체 접기";
    public string ShowMore => "더 보기";
    public string ShowLess => "접기";
    public string FilterAll => "전체";
    public string Folder => "폴더";
    public string Waiting => "대기 중";
    public string Tracking => "추적 중";
    public string AutoDetect => "자동 감지";
    public string Automation => "자동화";
    public string Layers => "레이어";
    public string Legend => "범례";

    // Newly added for MainWindow and Settings
    public string Quests => "퀘스트";
    public string Hideout => "은신처";
    public string Items => "아이템";
    public string Collector => "수집가 - 카파";
    public string Map => "지도";
    public string Profile => "프로필";
    public string PlayerLevel => "플레이어 레벨";
    public string ScavRep => "스캐브 평판";
    public string DspDecode => "DSP 디코드 횟수";
    public string Edition => "에디션";
    public string PrestigeLevel => "프레스티지 레벨";
    public string LoadingData => "데이터 로딩 중...";
    public string Initializing => "초기화 중...";
    public string LogFolder => "Tarkov 로그 폴더";
    public string QuestLogSync => "퀘스트 로그 동기화";
    public string CacheManagement => "캐시 관리";
    public string FontSize => "글꼴 크기";
    public string Font => "글꼴";
    public string FontDesc => "어플리케이션에 사용할 글꼴을 선택합니다. Fonts 폴더의 폰트 파일(.ttf, .otf)을 자동으로 인식합니다.";
    public string Unknown => "알 수 없음";

    public string UpdateApiData => "API 데이터 업데이트";
    public string ApiUpdateCheck => "업데이트 확인 중...";
    public string ApiUpdateSuccess => "성공적으로 업데이트되었습니다.";
    public string ApiUpdateFail => "업데이트 실패: {0}";
    public string ApiUpToDate => "이미 최신 버전입니다.";

    // Items Page Stats
    public string ItemsShowing => "아이템 {0}개 표시 중";

    // Map Page
    public string MapNoMapImage => "사용 가능한 지도 이미지가 없습니다";
    public string MapAddMapImageHint => "Assets/Maps/ 폴더에 이미지를 추가하세요";
    public string MapSetImagePathHint => "또는 설정에서 경로를 지정하세요";

    #endregion

    #region Common Name Localization (Trader, Map)

    public string GetLocalizedTraderName(string? englishName) => englishName?.ToLowerInvariant() switch
    {
        "prapor" => "프라포",
        "therapist" => "테라피스트",
        "skier" => "스키어",
        "peacekeeper" => "피스키퍼",
        "mechanic" => "메카닉",
        "ragman" => "래그맨",
        "jaeger" => "예거",
        "fence" => "펜스",
        "lightkeeper" => "등대지기",
        "ref" => "레프",
        "arena" => "아레나",
        "the labyrinth" => "미궁",
        "btr driver" => "BTR 운전수",
        _ => englishName ?? Unknown
    };

    public string GetLocalizedMapName(string? englishName) => englishName?.ToLowerInvariant().Replace("-", " ") switch
    {
        "customs" => "세관",
        "factory" => "공장",
        "interchange" => "인터체인지",
        "reserve" => "리저브",
        "shoreline" => "해안선",
        "woods" => "삼림",
        "lighthouse" => "등대",
        "streets" or "streets of tarkov" or "streetsoftarkov" => "타르코프 시내",
        "ground zero" or "groundzero" => "그라운드 제로",
        "the lab" or "the labs" or "labs" => "연구소",
        "the labyrinth" or "labyrinth" => "미궁",
        "arena" => "아레나",
        _ => englishName ?? Unknown
    };

    #endregion
}
