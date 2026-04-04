namespace TarkovHelper.Services;

/// <summary>
/// Items page specific localization strings.
/// </summary>
public partial class LocalizationService
{
    #region Items Page - Filter Labels

    public string ItemsSearchPlaceholder => "아이템 검색...";
    public string ItemsFilterAll => "전체";
    public string ItemsFilterQuest => "퀘스트";
    public string ItemsFilterHideout => "은신처";
    public string ItemsFilterAllCategories => "전체 카테고리";
    public string ItemsFilterAllStatus => "전체 상태";
    public string ItemsFilterNotStarted => "미시작";
    public string ItemsFilterInProgress => "진행 중";
    public string ItemsFilterFulfilled => "완료";
    public string ItemsFilterFirOnly => "FIR만";
    public string ItemsFilterHideFulfilled => "완료 숨기기";
    public string ItemsSortName => "이름";
    public string ItemsSortTotalCount => "총 수량";
    public string ItemsSortQuestCount => "퀘스트 수량";
    public string ItemsSortProgress => "진행도";

    #endregion

    #region Items Page - Column Headers

    public string ItemsHeaderItemName => "아이템 이름";
    public string ItemsHeaderQuest => "퀘스트";
    public string ItemsHeaderHideout => "은신처";
    public string ItemsHeaderTotal => "합계";
    public string ItemsHeaderNeed => "필요";
    public string ItemsHeaderOwned => "보유:";

    #endregion

    #region Items Page - Detail Panel

    public string ItemsSelectItem => "아이템을 선택하면 상세 정보가 표시됩니다";
    public string ItemsOpenWiki => "위키 열기";
    public string ItemsYourInventory => "보유 아이템";
    public string ItemsProgress => "진행도";
    public string ItemsRequiredForQuests => "퀘스트 필요 항목";
    public string ItemsRequiredForHideout => "은신처 필요 항목";
    public string ItemsLevel => "레벨";

    #endregion

    #region Items Page - Loading

    public string ItemsLoading => "아이템 데이터 로딩 중...";

    #endregion

    #region Item Categories - Parent Categories

    /// <summary>
    /// Get localized category name. Returns English name as fallback for unknown categories.
    /// </summary>
    public string GetCategoryName(string categoryKey)
    {
        return GetCategoryNameKO(categoryKey);
    }

    private static string GetCategoryNameKO(string key) => key switch
    {
        "All Categories" => "전체 카테고리",
        "Provisions" => "식량",
        "Medical" => "의료품",
        "Gear" => "장비",
        "Barter" => "물물교환",
        "Info & Keys" => "정보 & 열쇠",
        "Containers" => "컨테이너",
        "Money" => "화폐",
        "Ammo" => "탄약",
        "Weapon Mods" => "무기 부품",
        "Optics" => "광학장비",
        "Tactical" => "전술장비",
        "Helmet Mods" => "헬멧 부품",
        "Weapons" => "무기",
        "Quest Items" => "퀘스트 아이템",
        "Misc" => "기타",
        "Other" => "기타",
        _ => key // Fallback to English
    };

    #endregion
}
