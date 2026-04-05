using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;
using TarkovHelper.Models;
using TarkovHelper.Models.Map;
using TarkovHelper.Services;
using TarkovHelper.Services.Logging;
using TarkovHelper.Services.Map;
using TarkovHelper.Services.Settings;

namespace TarkovHelper.Pages.Map.Components;

/// <summary>
/// 맵 페이지의 Map Markers (PMC Spawn, Sniper Scav, Rogue, Cultist, Lever) 관리를 담당하는 클래스.
/// 마커 생성, 필터링, 표시 등의 로직을 캡슐화합니다.
/// </summary>
public class MapMarkersManager
{
    private static readonly ILogger _log = Log.For<MapMarkersManager>();

    // 의존성 서비스
    private readonly Canvas _markersContainer;
    private readonly MapTrackerService _trackerService;
    private readonly MapMarkerDbService _markerDbService;
    private readonly LocalizationService _loc;
    private readonly MapSettings _settings;

    // 마커 상태
    private readonly List<FrameworkElement> _markerElements = new();

    // SVG 아이콘 캐시
    private readonly Dictionary<MarkerType, DrawingGroup?> _iconCache = new();
    private readonly string _iconBasePath;

    // 설정 및 상태
    private string? _currentMapKey;
    private string? _currentFloorId;
    private double _zoomLevel = 1.0;

    // 가시성 설정 (MapSettings와 연동)
    private bool _showPmcSpawns = true;
    private bool _showSniperScavs = true;
    private bool _showRogues = true;
    private bool _showCultists = true;
    private bool _showLevers = true;
    private bool _showBosses = true;

    public MapMarkersManager(
        Canvas markersContainer,
        MapTrackerService trackerService,
        MapMarkerDbService markerDbService,
        LocalizationService localizationService)
    {
        _markersContainer = markersContainer ?? throw new ArgumentNullException(nameof(markersContainer));
        _trackerService = trackerService ?? throw new ArgumentNullException(nameof(trackerService));
        _markerDbService = markerDbService ?? throw new ArgumentNullException(nameof(markerDbService));
        _loc = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _settings = MapSettings.Instance;

        // SVG 아이콘 경로
        _iconBasePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "DB", "Icons", "Markers");

        // 설정값 로드
        LoadSettingsFromMapSettings();
    }

    #region Public Methods - Configuration

    public void SetCurrentMap(string? mapKey)
    {
        _currentMapKey = mapKey;
    }

    public void SetCurrentFloor(string? floorId)
    {
        _currentFloorId = floorId;
    }

    public void SetZoomLevel(double zoomLevel)
    {
        _zoomLevel = zoomLevel;
    }

    public void SetShowPmcSpawns(bool show)
    {
        _showPmcSpawns = show;
        _settings.ShowPmcSpawns = show;
    }

    public void SetShowSniperScavs(bool show)
    {
        _showSniperScavs = show;
        _settings.ShowSniperScavs = show;
    }

    public void SetShowRogues(bool show)
    {
        _showRogues = show;
        _settings.ShowRogues = show;
    }

    public void SetShowCultists(bool show)
    {
        _showCultists = show;
        _settings.ShowCultists = show;
    }

    public void SetShowLevers(bool show)
    {
        _showLevers = show;
        _settings.ShowLevers = show;
    }

    public void SetShowBosses(bool show)
    {
        _showBosses = show;
        _settings.ShowBosses = show;
    }

    /// <summary>
    /// MapSettings에서 설정값 로드
    /// </summary>
    public void LoadSettingsFromMapSettings()
    {
        _showPmcSpawns = _settings.ShowPmcSpawns;
        _showSniperScavs = _settings.ShowSniperScavs;
        _showRogues = _settings.ShowRogues;
        _showCultists = _settings.ShowCultists;
        _showLevers = _settings.ShowLevers;
        _showBosses = _settings.ShowBosses;
    }

    #endregion

    #region Public Methods - Marker Management

    public async Task RefreshMarkersAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_currentMapKey)) return;
        if (!_markerDbService.IsLoaded) return;

        // 기존 마커 제거
        ClearMarkers();

        // 맵 설정 가져오기
        var config = _trackerService.GetMapConfig(_currentMapKey);
        if (config == null) return;

        // 현재 맵의 마커 가져오기
        var markers = _markerDbService.GetMarkersForMap(_currentMapKey);

        int count = 0;
        foreach (var marker in markers)
        {
            // [v1.1.37] UI 부하 분산
            count++;
            if (count % 30 == 0) await Task.Yield();

            ct.ThrowIfCancellationRequested();

            // 마커 타입별 가시성 확인
            if (!ShouldShowMarker(marker.Type)) continue;

            // 좌표 변환
            var (screenX, screenY) = config.GameToScreenForPlayer(marker.X, marker.Z);

            // 층 정보 확인
            var isOnCurrentFloor = IsMarkerOnCurrentFloor(marker.FloorId);

            // 마커 생성
            var markerElement = CreateMarker(marker, screenX, screenY, isOnCurrentFloor);
            _markerElements.Add(markerElement);
            _markersContainer.Children.Add(markerElement);
        }

        _log.Debug($"Rendered {_markerElements.Count} map markers for '{_currentMapKey}'");
    }

    public void ClearMarkers()
    {
        _markerElements.Clear();
        _markersContainer.Children.Clear();
    }

    public void UpdateMarkerScales()
    {
        var inverseScale = 1.0 / _zoomLevel;

        foreach (var marker in _markerElements)
        {
            if (marker is Canvas canvas)
            {
                canvas.RenderTransform = new ScaleTransform(inverseScale, inverseScale);
            }
        }
    }

    /// <summary>
    /// 특정 마커 타입의 가시성 업데이트 (체크박스 토글 시 호출)
    /// </summary>
    public void UpdateMarkerVisibility(MarkerType type, bool visible)
    {
        foreach (var element in _markerElements)
        {
            if (element is Canvas canvas && canvas.Tag is MapMarker marker)
            {
                if (marker.Type == type)
                {
                    canvas.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }
    }

    #endregion

    #region Private Methods - Marker Creation

    private FrameworkElement CreateMarker(MapMarker marker, double screenX, double screenY, bool isOnCurrentFloor)
    {
        // 맵별 마커 스케일 적용
        var mapConfig = _trackerService.GetMapConfig(_currentMapKey ?? "");
        var mapScale = mapConfig?.MarkerScale ?? 1.0;

        var baseSize = 24.0;
        var markerSize = baseSize * mapScale;

        // 마커 색상
        var (r, g, b) = MapMarker.GetMarkerColor(marker.Type);
        var markerColor = Color.FromRgb(r, g, b);

        // 다른 층의 마커는 색상을 흐리게 처리
        if (!isOnCurrentFloor)
        {
            markerColor = Color.FromArgb(100, markerColor.R, markerColor.G, markerColor.B);
        }

        var canvas = new Canvas
        {
            Width = 0,
            Height = 0,
            Tag = marker
        };

        // SVG 아이콘 로드 시도
        var iconDrawing = GetOrLoadIcon(marker.Type);
        if (iconDrawing != null)
        {
            // SVG 아이콘 사용
            var drawingImage = new DrawingImage(iconDrawing);
            var iconImage = new Image
            {
                Source = drawingImage,
                Width = markerSize,
                Height = markerSize,
                Stretch = Stretch.Uniform,
                IsHitTestVisible = false // 마우스 이벤트를 Canvas로 전달
            };

            Canvas.SetLeft(iconImage, -markerSize / 2);
            Canvas.SetTop(iconImage, -markerSize / 2);
            canvas.Children.Add(iconImage);
        }
        else
        {
            // 폴백: 원형 마커
            var glowSize = markerSize * 1.5;
            var glow = new Ellipse
            {
                Width = glowSize,
                Height = glowSize,
                Fill = new SolidColorBrush(Color.FromArgb(80, markerColor.R, markerColor.G, markerColor.B))
            };
            Canvas.SetLeft(glow, -glowSize / 2);
            Canvas.SetTop(glow, -glowSize / 2);
            canvas.Children.Add(glow);

            var mainCircle = new Ellipse
            {
                Width = markerSize,
                Height = markerSize,
                Fill = new SolidColorBrush(markerColor),
                Stroke = Brushes.White,
                StrokeThickness = 2
            };
            Canvas.SetLeft(mainCircle, -markerSize / 2);
            Canvas.SetTop(mainCircle, -markerSize / 2);
            canvas.Children.Add(mainCircle);
        }

        // 위치 설정
        Canvas.SetLeft(canvas, screenX);
        Canvas.SetTop(canvas, screenY);

        // 줌에 상관없이 고정 크기 유지를 위한 역스케일 적용
        var inverseScale = 1.0 / _zoomLevel;
        canvas.RenderTransform = new ScaleTransform(inverseScale, inverseScale);
        canvas.RenderTransformOrigin = new Point(0, 0);

        // 다른 층의 마커는 반투명 처리
        if (!isOnCurrentFloor)
        {
            canvas.Opacity = 0.5;
        }

        // Boss와 Lever만 마우스 호버 시 이름 오버레이 표시, 나머지는 Mouse Through
        if (marker.Type == MarkerType.BossSpawn || marker.Type == MarkerType.Lever)
        {
            // 히트 테스트용 투명 원 추가 (마우스 이벤트 수신 영역)
            var hitArea = new Ellipse
            {
                Width = markerSize,
                Height = markerSize,
                Fill = Brushes.Transparent
            };
            Canvas.SetLeft(hitArea, -markerSize / 2);
            Canvas.SetTop(hitArea, -markerSize / 2);
            canvas.Children.Add(hitArea);

            var displayName = GetLocalizedName(marker);

            // 이름 라벨과 층 뱃지를 담을 StackPanel (기본 숨김, 줌 레벨 무관 고정 크기)
            var labelStackPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false,
                // 줌 레벨에 따른 역스케일 적용 (고정 크기 유지)
                RenderTransform = new ScaleTransform(_zoomLevel, _zoomLevel),
                RenderTransformOrigin = new Point(0.5, 1.0)
            };

            // 이름 라벨 (고정 크기)
            var nameLabel = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(220, 30, 30, 30)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 4, 8, 4),
                Child = new TextBlock
                {
                    Text = displayName,
                    FontSize = 24,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brushes.White,
                    TextAlignment = TextAlignment.Center
                }
            };
            labelStackPanel.Children.Add(nameLabel);

            // 층 뱃지 추가 (다른 층일 때만, 고정 크기)
            var floorInfo = GetFloorIndicator(marker.FloorId);
            if (floorInfo.HasValue)
            {
                var (arrow, floorText, indicatorColor) = floorInfo.Value;

                var floorBadge = new Border
                {
                    Background = new SolidColorBrush(indicatorColor),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(6, 4, 6, 4),
                    Margin = new Thickness(4, 0, 0, 0),
                    Child = new TextBlock
                    {
                        Text = $"{arrow}{floorText}",
                        FontSize = 20,
                        FontWeight = FontWeights.Bold,
                        Foreground = Brushes.White,
                        TextAlignment = TextAlignment.Center
                    }
                };
                labelStackPanel.Children.Add(floorBadge);
            }

            // 라벨 위치 설정 (마커 위에 표시)
            Canvas.SetLeft(labelStackPanel, 0);
            Canvas.SetTop(labelStackPanel, -markerSize - 40);
            canvas.Children.Add(labelStackPanel);

            // 마우스 호버 이벤트로 라벨 표시/숨김
            hitArea.MouseEnter += (s, e) => labelStackPanel.Visibility = Visibility.Visible;
            hitArea.MouseLeave += (s, e) => labelStackPanel.Visibility = Visibility.Collapsed;

            canvas.Cursor = Cursors.Hand;
        }
        else
        {
            // PMC Spawn, Sniper Scav, Rogue, Cultist 등은 마우스 이벤트 무시
            canvas.IsHitTestVisible = false;
        }

        return canvas;
    }

    private DrawingGroup? GetOrLoadIcon(MarkerType type)
    {
        if (_iconCache.TryGetValue(type, out var cached))
        {
            return cached;
        }

        var svgFileName = MapMarker.GetSvgIconFileName(type);
        if (string.IsNullOrEmpty(svgFileName))
        {
            _iconCache[type] = null;
            return null;
        }

        var svgPath = System.IO.Path.Combine(_iconBasePath, svgFileName);
        if (!File.Exists(svgPath))
        {
            _log.Warning($"SVG icon not found: {svgPath}");
            _iconCache[type] = null;
            return null;
        }

        try
        {
            var settings = new WpfDrawingSettings
            {
                IncludeRuntime = true,
                TextAsGeometry = false
            };

            using var reader = new FileSvgReader(settings);
            var drawing = reader.Read(svgPath);
            _iconCache[type] = drawing;
            return drawing;
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to load SVG icon: {svgPath}", ex);
            _iconCache[type] = null;
            return null;
        }
    }

    #endregion

    #region Private Methods - Helpers

    private bool ShouldShowMarker(MarkerType type)
    {
        return type switch
        {
            MarkerType.PmcSpawn => _showPmcSpawns,
            MarkerType.SniperScavSpawn => _showSniperScavs,
            MarkerType.RogueSpawn => _showRogues,
            MarkerType.CultistSpawn => _showCultists,
            MarkerType.Lever => _showLevers,
            MarkerType.BossSpawn => _showBosses,
            _ => false // 다른 타입은 이 매니저에서 처리하지 않음
        };
    }

    private bool IsMarkerOnCurrentFloor(string? markerFloorId)
    {
        // 단일 층 맵이거나 층 선택이 없는 경우: 모든 마커를 현재 층으로 간주
        if (string.IsNullOrEmpty(_currentFloorId))
            return true;

        // 마커에 층 정보가 없는 경우: 기본 층(main)으로 간주
        if (string.IsNullOrEmpty(markerFloorId))
        {
            // 현재 선택된 층이 main이면 표시, 아니면 다른 층으로 처리
            return string.Equals(_currentFloorId, "main", StringComparison.OrdinalIgnoreCase);
        }

        // 층 ID 비교 (대소문자 무시)
        return string.Equals(_currentFloorId, markerFloorId, StringComparison.OrdinalIgnoreCase);
    }

    private string GetLocalizedName(MapMarker marker)
    {
        return !string.IsNullOrEmpty(marker.NameKo) ? marker.NameKo : marker.Name;
    }

    private (string arrow, string floorText, Color color)? GetFloorIndicator(string? markerFloorId)
    {
        if (string.IsNullOrEmpty(_currentMapKey) || string.IsNullOrEmpty(_currentFloorId))
            return null;

        var config = _trackerService.GetMapConfig(_currentMapKey);
        if (config?.Floors == null || config.Floors.Count == 0)
            return null;

        // 현재 층의 Order 가져오기
        var currentFloor = config.Floors.FirstOrDefault(f =>
            string.Equals(f.LayerId, _currentFloorId, StringComparison.OrdinalIgnoreCase));
        var currentOrder = currentFloor?.Order ?? 0;

        // 마커 층의 Order 가져오기 (FloorId가 없으면 main으로 간주)
        var effectiveFloorId = string.IsNullOrEmpty(markerFloorId) ? "main" : markerFloorId;
        var markerFloor = config.Floors.FirstOrDefault(f =>
            string.Equals(f.LayerId, effectiveFloorId, StringComparison.OrdinalIgnoreCase));
        var markerOrder = markerFloor?.Order ?? 0;

        // 같은 층이면 표시 안함
        if (currentOrder == markerOrder)
            return null;

        // 화살표 방향 결정 (마커가 현재 층보다 위에 있으면 ↑, 아래면 ↓)
        var isAbove = markerOrder > currentOrder;
        var arrow = isAbove ? "↑" : "↓";

        // 색상 결정 (위: 하늘색, 아래: 주황색)
        var color = isAbove
            ? Color.FromRgb(100, 181, 246)  // Light Blue
            : Color.FromRgb(255, 167, 38);  // Orange

        // 층 표시 문자 결정 (B: 지하, G: 기본층, 2F/3F: 2층/3층)
        string floorText;
        if (markerOrder < 0)
        {
            floorText = "B";
        }
        else if (markerOrder == 0)
        {
            floorText = "G";
        }
        else
        {
            floorText = $"{markerOrder + 1}F"; // Order 1 = 2F
        }

        return (arrow, floorText, color);
    }

    #endregion
}
