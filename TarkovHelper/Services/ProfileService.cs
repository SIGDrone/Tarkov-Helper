using System;
using TarkovHelper.Models;

namespace TarkovHelper.Services;

/// <summary>
/// 현재 활성화된 프로필(PVP 또는 PVE)을 관리하고 변경 시 알림을 제공하는 서비스.
/// </summary>
public sealed class ProfileService
{
    private static ProfileService? _instance;
    public static ProfileService Instance
    {
        get
        {
            if (_instance == null) _instance = new ProfileService();
            return _instance;
        }
    }

    private ProfileType? _currentProfile;

    public ProfileType CurrentProfile
    {
        get
        {
            if (_currentProfile == null)
            {
                LoadProfileFromDb();
            }
            return _currentProfile ?? ProfileType.Pvp;
        }
        set
        {
            if (_currentProfile != value)
            {
                System.Diagnostics.Debug.WriteLine($"[ProfileService] Profile Changing: {_currentProfile} -> {value}");
                _currentProfile = value;
                // 직접 DB에 마지막 프로필 상태 저장 (전역 영역 null)
                UserDataDbService.Instance.SetSetting("app.lastProfileType", value.ToString(), null);
                ProfileChanged?.Invoke(this, value);
            }
        }
    }

    /// <summary>
    /// 프로필이 변경되었을 때 발생하는 이벤트.
    /// </summary>
    public event EventHandler<ProfileType>? ProfileChanged;

    private ProfileService() 
    { 
        _instance = this;
    }

    private void LoadProfileFromDb()
    {
        try
        {
            var lastProfile = UserDataDbService.Instance.GetSetting("app.lastProfileType", null);
            System.Diagnostics.Debug.WriteLine($"[ProfileService] DB Load result: '{lastProfile}'");

            if (Enum.TryParse<ProfileType>(lastProfile, true, out var type))
            {
                _currentProfile = type;
                System.Diagnostics.Debug.WriteLine($"[ProfileService] Loaded Profile: {type}");
            }
            else
            {
                _currentProfile = ProfileType.Pvp;
                System.Diagnostics.Debug.WriteLine($"[ProfileService] Load failed or empty, fallback to PVP");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfileService] Load Fatal Error: {ex.Message}");
            // 실패 시 별도 로그 파일에 기록 (PVP로 돌아가는 이유 확인)
            try { 
                var logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup_error.log");
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] Profile Load Error: {ex}\n");
            } catch { }
            
            _currentProfile = ProfileType.Pvp;
        }
    }

    /// <summary>
    /// 지정된 프로필 타입에 따라 표시용 텍스트를 반환합니다.
    /// </summary>
    public string GetProfileName(ProfileType type) => type switch
    {
        ProfileType.Pvp => "PVP",
        ProfileType.Pve => "PVE",
        _ => "UNKNOWN"
    };
}
