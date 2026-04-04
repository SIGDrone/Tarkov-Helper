using System;
using TarkovHelper.Models;

namespace TarkovHelper.Services;

/// <summary>
/// 현재 활성화된 프로필(PVP 또는 PVE)을 관리하고 변경 시 알림을 제공하는 서비스.
/// </summary>
public sealed class ProfileService
{
    private static ProfileService? _instance;
    public static ProfileService Instance => _instance ??= new ProfileService();

    private ProfileType _currentProfile = ProfileType.Pvp;

    public ProfileType CurrentProfile
    {
        get => _currentProfile;
        set
        {
            if (_currentProfile != value)
            {
                _currentProfile = value;
                ProfileChanged?.Invoke(this, value);
            }
        }
    }

    /// <summary>
    /// 프로필이 변경되었을 때 발생하는 이벤트.
    /// </summary>
    public event EventHandler<ProfileType>? ProfileChanged;

    private ProfileService() { }

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
