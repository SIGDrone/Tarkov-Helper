using System;

namespace TarkovHelper.Models.Map;

/// <summary>
/// 사용자가 직접 추가한 커스텀 맵 마커 정보
/// </summary>
public class CustomMapMarker
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string MapKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
    public string? FloorId { get; set; }
    public string? Color { get; set; }
    public double Size { get; set; } = 24.0;
    public double Opacity { get; set; } = 1.0;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public CustomMapMarker() { }

    public CustomMapMarker(string mapKey, string name, double x, double y, double z, string? floorId = null)
    {
        MapKey = mapKey;
        Name = name;
        X = x;
        Y = y;
        Z = z;
        FloorId = floorId;
        Color = "#FFD700"; // 기본 금색
        Size = 24.0;
        Opacity = 1.0;
    }
}
